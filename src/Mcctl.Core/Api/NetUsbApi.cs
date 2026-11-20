using System.Diagnostics;
using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>netusb/*</c> endpoints: network sources, streaming services, USB, server, Bluetooth, AirPlay.</summary>
public sealed class NetUsbApi(YxcClient client)
{
    /// <summary>Maximum page size accepted by getListInfo.</summary>
    public const int MaxListPageSize = 8;

    public Task<NetUsbPlayInfo> GetPlayInfoAsync(CancellationToken ct = default)
        => client.GetAsync<NetUsbPlayInfo>("netusb/getPlayInfo", null, ct);

    public Task<YxcResponse> SetPlaybackAsync(PlaybackCommand command, CancellationToken ct = default)
        => client.GetAsync("netusb/setPlayback", Query.Of("playback", command), ct);

    /// <summary>Seek to a position in seconds (only when the source supports it).</summary>
    public Task<YxcResponse> SetPlayPositionAsync(int seconds, CancellationToken ct = default)
        => client.GetAsync("netusb/setPlayPosition", Query.Of("position", seconds), ct);

    public Task<YxcResponse> ToggleRepeatAsync(CancellationToken ct = default)
        => client.GetAsync("netusb/toggleRepeat", null, ct);

    public Task<YxcResponse> ToggleShuffleAsync(CancellationToken ct = default)
        => client.GetAsync("netusb/toggleShuffle", null, ct);

    /// <param name="mode">"off", "one", "all".</param>
    public Task<YxcResponse> SetRepeatAsync(string mode, CancellationToken ct = default)
        => client.GetAsync("netusb/setRepeat", Query.Of("mode", mode), ct);

    /// <param name="mode">"off", "on", "songs", "albums".</param>
    public Task<YxcResponse> SetShuffleAsync(string mode, CancellationToken ct = default)
        => client.GetAsync("netusb/setShuffle", Query.Of("mode", mode), ct);

    /// <summary>Gets one page (max 8 lines) of the current browse list of <paramref name="input"/>.</summary>
    public Task<ListInfo> GetListInfoAsync(string input, int index = 0, int size = MaxListPageSize, string lang = "en", CancellationToken ct = default)
        => client.GetAsync<ListInfo>("netusb/getListInfo",
            new Query().Add("input", input).Add("index", index).Add("size", Math.Clamp(size, 1, MaxListPageSize)).Add("lang", lang), ct);

    /// <summary>Reads every line of the current browse list by paging through it.</summary>
    public async Task<ListInfo> GetFullListAsync(string input, string lang = "en", int maxItems = 2000, CancellationToken ct = default)
    {
        var first = await GetListInfoAsync(input, 0, MaxListPageSize, lang, ct).ConfigureAwait(false);
        var total = Math.Min(first.MaxLine, maxItems);
        while (first.ListInfoItems.Count < total)
        {
            var page = await GetListInfoAsync(input, first.ListInfoItems.Count, MaxListPageSize, lang, ct).ConfigureAwait(false);
            if (page.ListInfoItems.Count == 0)
            {
                break;
            }

            first.ListInfoItems.AddRange(page.ListInfoItems);
        }

        return first;
    }

    /// <summary>Navigates the browse list.</summary>
    /// <param name="type">select (enter folder / start item), play, return (go up one layer).</param>
    /// <param name="index">Absolute line index (required for select/play, ignored for return).</param>
    public Task<YxcResponse> SetListControlAsync(ListControlType type, int? index = null, string zone = ZoneIds.Main, string listId = "main", CancellationToken ct = default)
        => client.GetAsync("netusb/setListControl", new Query()
            .Add("list_id", listId)
            .Add("type", type)
            .Add("index", type == ListControlType.Return ? null : index)
            .Add("zone", zone), ct);

    // ------------------------------------------------------------------
    // Browse helpers.
    // The device keeps a single browse cursor ("list_id=main") that is bound to the input last
    // passed to getListInfo, and it loads a new list asynchronously after select/return
    // (about 1 s for a NAS folder). These helpers bind the cursor first and wait for the change.
    // ------------------------------------------------------------------

    /// <summary>Enters folder <paramref name="index"/> of <paramref name="input"/>'s list and waits until the new list is loaded.</summary>
    public async Task<ListInfo> EnterFolderAsync(string input, int index, string zone = ZoneIds.Main, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        try
        {
            return await SelectAndWaitAsync(input, index, zone, timeout, ct).ConfigureAwait(false);
        }
        catch (YxcException ex) when (ex.ResponseCode == AccessError)
        {
            // A sleeping media server (NAS) fails the first attempt while it wakes up; the second one succeeds.
            return await SelectAndWaitAsync(input, index, zone, timeout, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Goes up one layer of <paramref name="input"/>'s list (no-op at the top) and waits for the new list.</summary>
    public async Task<ListInfo> ReturnAsync(string input, string zone = ZoneIds.Main, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var before = await BindListAsync(input, ct).ConfigureAwait(false);
        if (before.MenuLayer == 0)
        {
            return before;
        }

        await SetListControlAsync(ListControlType.Return, zone: zone, ct: ct).ConfigureAwait(false);
        return await WaitForListChangeAsync(input, before, timeout, ct).ConfigureAwait(false);
    }

    /// <summary>Plays item <paramref name="index"/> of <paramref name="input"/>'s list on <paramref name="zone"/>.</summary>
    public async Task PlayItemAsync(string input, int index, string zone = ZoneIds.Main, CancellationToken ct = default)
    {
        await BindListAsync(input, index, ct).ConfigureAwait(false);
        try
        {
            await SetListControlAsync(ListControlType.Play, index, zone, ct: ct).ConfigureAwait(false);
        }
        catch (YxcException ex) when (ex.ResponseCode is 3 or 4)
        {
            // Some services only accept "select" to start an item.
            await SetListControlAsync(ListControlType.Select, index, zone, ct: ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a search from a searchable list item (attribute <see cref="ListItemAttribute.CapableOfSearch"/>,
    /// e.g. Net Radio → Radio → 検索する) and waits until the result list is shown.
    /// </summary>
    /// <remarks>
    /// Sequence verified on R-N1000A: <c>select</c> the search item, then immediately <c>setSearchString</c>
    /// without an index. Reading the list in between makes the device leave the waiting-for-input state
    /// (it falls back to the top layer), and passing <c>index</c> selects a different list entry instead.
    /// </remarks>
    /// <param name="index">Line index of the search item in the current list.</param>
    public async Task<ListInfo> SearchAsync(string input, int index, string text, string zone = ZoneIds.Main, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var before = await BindListAsync(input, index, ct).ConfigureAwait(false);
        await SetListControlAsync(ListControlType.Select, index, zone, ct: ct).ConfigureAwait(false);

        // The device needs a moment to enter the search-input state; a string sent right away is dropped.
        await Task.Delay(SearchInputDelay, ct).ConfigureAwait(false);
        await SetSearchStringAsync(text, ct: ct).ConfigureAwait(false);
        return await WaitForListChangeAsync(input, before, timeout, ct).ConfigureAwait(false);
    }

    internal TimeSpan SearchInputDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    private const int AccessError = 100;

    private async Task<ListInfo> SelectAndWaitAsync(string input, int index, string zone, TimeSpan? timeout, CancellationToken ct)
    {
        var before = await BindListAsync(input, index, ct).ConfigureAwait(false);
        await SetListControlAsync(ListControlType.Select, index, zone, ct: ct).ConfigureAwait(false);
        return await WaitForListChangeAsync(input, before, timeout, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Points the device's single browse cursor at <paramref name="input"/> and returns its current state.
    /// The page containing <paramref name="index"/> is fetched because the device resolves the next
    /// select/play index against the page it returned last: if the item is not in that page it falls back to a
    /// stale cached list (observed on R-N1000A: selecting "Search" opened "Podcasts" from the top layer).
    /// </summary>
    private Task<ListInfo> BindListAsync(string input, int index, CancellationToken ct)
        => GetListInfoAsync(input, index - index % MaxListPageSize, MaxListPageSize, ct: ct);

    private Task<ListInfo> BindListAsync(string input, CancellationToken ct) => BindListAsync(input, 0, ct);

    /// <summary>
    /// Polls until the list differs from <paramref name="before"/>.
    /// While loading, the device may answer Initializing / Guarded / Access error; if it then reports the
    /// original list again, the navigation was abandoned and that error is thrown instead of waiting for the timeout.
    /// </summary>
    internal async Task<ListInfo> WaitForListChangeAsync(string input, ListInfo before, TimeSpan? timeout, CancellationToken ct)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        var started = Stopwatch.GetTimestamp();
        long? changedAt = null;
        var current = before;
        YxcException? loadError = null;

        // Polling right after select can make the device abandon a slow (NAS) load, so give it a head start.
        var delay = ListFirstPollDelay;
        while (Stopwatch.GetElapsedTime(started) < limit)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            delay = ListPollInterval;
            try
            {
                current = await GetListInfoAsync(input, 0, 1, ct: ct).ConfigureAwait(false);
            }
            catch (YxcException ex) when (ex.ResponseCode is 1 or 5 or AccessError)
            {
                loadError = ex;
                continue;
            }

            if (current.MenuLayer == before.MenuLayer && current.MenuName == before.MenuName && current.MaxLine == before.MaxLine)
            {
                if (loadError is not null)
                {
                    throw loadError; // failed and fell back to the original list
                }

                continue;
            }

            // An empty list right after the switch is usually still loading; give it a moment.
            changedAt ??= Stopwatch.GetTimestamp();
            if (current.MaxLine > 0 || Stopwatch.GetElapsedTime(changedAt.Value) > EmptyListGrace)
            {
                return current;
            }
        }

        return loadError is null ? current : throw loadError;
    }


    private static readonly TimeSpan EmptyListGrace = TimeSpan.FromSeconds(1.5);

    internal TimeSpan ListFirstPollDelay { get; set; } = TimeSpan.FromSeconds(1);

    internal TimeSpan ListPollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Starts a search inside a searchable list item (e.g. Net Radio, streaming services).</summary>
    public Task<YxcResponse> SetSearchStringAsync(string text, int? index = null, string listId = "main", CancellationToken ct = default)
        => client.PostAsync("netusb/setSearchString", new { list_id = listId, @string = text, index }, ct);

    public Task<NetUsbPresetInfo> GetPresetInfoAsync(CancellationToken ct = default)
        => client.GetAsync<NetUsbPresetInfo>("netusb/getPresetInfo", null, ct);

    /// <param name="num">1-based preset number.</param>
    public Task<YxcResponse> RecallPresetAsync(int num, string zone = ZoneIds.Main, CancellationToken ct = default)
        => client.GetAsync("netusb/recallPreset", new Query().Add("zone", zone).Add("num", num), ct);

    /// <summary>Stores what is playing now into preset <paramref name="num"/> (1-based).</summary>
    public Task<YxcResponse> StorePresetAsync(int num, CancellationToken ct = default)
        => client.GetAsync("netusb/storePreset", Query.Of("num", num), ct);

    public Task<YxcResponse> ClearPresetAsync(int num, CancellationToken ct = default)
        => client.GetAsync("netusb/clearPreset", Query.Of("num", num), ct);

    public Task<RecentInfo> GetRecentInfoAsync(CancellationToken ct = default)
        => client.GetAsync<RecentInfo>("netusb/getRecentInfo", null, ct);

    public Task<YxcResponse> RecallRecentItemAsync(int num, string zone = ZoneIds.Main, CancellationToken ct = default)
        => client.GetAsync("netusb/recallRecentItem", new Query().Add("zone", zone).Add("num", num), ct);

    public Task<YxcResponse> ClearRecentInfoAsync(CancellationToken ct = default)
        => client.GetAsync("netusb/clearRecentInfo", null, ct);

    /// <summary>Adds / removes the current track to bookmarks or likes (service dependent).</summary>
    /// <param name="type">"add_bookmark", "add_track", "add_album", "thumbs_up", "thumbs_down", "mark_tired".</param>
    public Task<YxcResponse> ManagePlayAsync(string type, int timeout = 5000, CancellationToken ct = default)
        => client.GetAsync("netusb/managePlay", new Query().Add("type", type).Add("timeout", timeout), ct);
}

/// <summary>Playback commands; sent as snake_case (<c>PlayPause</c> → <c>play_pause</c>).</summary>
public enum PlaybackCommand
{
    Play,
    Stop,
    Pause,
    PlayPause,
    Previous,
    Next,
    FastReverseStart,
    FastReverseEnd,
    FastForwardStart,
    FastForwardEnd,
}

/// <summary><c>setListControl</c> types; sent as lowercase.</summary>
public enum ListControlType
{
    Select,
    Play,
    Return,
}
