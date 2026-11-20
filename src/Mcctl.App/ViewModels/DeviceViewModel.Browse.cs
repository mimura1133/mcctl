using System.Collections.ObjectModel;
using Mcctl.App.Mvvm;
using Mcctl.Events;
using Mcctl.Models;

namespace Mcctl.App.ViewModels;

/// <summary>Browsing the netusb list (Server, Net Radio, radiko, streaming services …).</summary>
public sealed partial class DeviceViewModel
{
    private string? _browseInput;
    private int _browseGeneration;

    public ObservableCollection<BrowseItem> BrowseItems { get; } = [];

    public string MenuName { get; private set => Set(ref field, value); } = "";

    public int MenuLayer { get; private set => Set(ref field, value); }

    public bool BrowseBusy { get; private set => Set(ref field, value); }

    public AsyncCommand BrowseOpenCommand => field ??= new AsyncCommand<BrowseItem>(OpenBrowseItemAsync);
    public AsyncCommand BrowsePlayCommand => field ??= new AsyncCommand<BrowseItem>(PlayBrowseItemAsync);
    public AsyncCommand BrowseBackCommand => field ??= new AsyncCommand(BrowseBackAsync, () => MenuLayer > 0);
    public AsyncCommand BrowseRefreshCommand => field ??= new AsyncCommand(RefreshBrowseAsync);

    public async Task RefreshBrowseAsync()
    {
        var input = CurrentInput;
        var generation = ++_browseGeneration;
        if (input != _browseInput)
        {
            PendingSearch = null; // a search box belongs to the list it was opened from
        }

        _browseInput = input;

        if (!IsNetUsb || string.IsNullOrEmpty(input))
        {
            ShowBrowseList("", 0, []);
            return;
        }

        BrowseBusy = true;
        try
        {
            var list = await Device.NetUsb.GetFullListAsync(input, lang: "ja", maxItems: 1000);
            if (generation != _browseGeneration)
            {
                return; // a newer reload (e.g. another input) superseded this one
            }

            ShowBrowseList(
                string.IsNullOrEmpty(list.MenuName) ? DisplayNames.Get(input, _names) : list.MenuName,
                list.MenuLayer,
                list.ListInfoItems.Select((it, i) => new BrowseItem(i, it.Text, Device.ResolveUrl(it.Thumbnail), it.IsContainer, it.IsPlayable,
                    i == list.PlayingIndex || it.Flags.HasFlag(ListItemAttribute.NowPlaying), it.IsSearchable)));
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            if (generation == _browseGeneration)
            {
                // Inputs such as Bluetooth / AirPlay / USB DAC have no browsable list.
                ShowBrowseList(DisplayNames.Get(input, _names), 0, []);
            }
        }
        finally
        {
            if (generation == _browseGeneration)
            {
                BrowseBusy = false;
            }
        }
    }

    private void ShowBrowseList(string menuName, int layer, IEnumerable<BrowseItem> items)
    {
        MenuName = menuName;
        MenuLayer = layer;
        ReplaceAll(BrowseItems, items);
    }

    // ---------- search (e.g. Net Radio → Radio → 検索する) ----------

    /// <summary>Search entry the user opened; non-null while the search box is shown.</summary>
    public BrowseItem? PendingSearch
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(IsSearchPromptVisible));
            }
        }
    }

    public bool IsSearchPromptVisible => PendingSearch is not null;

    public string SearchText
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                // Enable or disable the 検索 button right away, not only on the next input event.
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    } = "";

    public AsyncCommand RunSearchCommand => field ??= new AsyncCommand(RunSearchAsync, () => PendingSearch is not null && !string.IsNullOrWhiteSpace(SearchText));

    public RelayCommand CancelSearchCommand => field ??= new RelayCommand(() => PendingSearch = null);

    private async Task RunSearchAsync()
    {
        if (PendingSearch is not { } item)
        {
            return;
        }

        var text = SearchText.Trim();
        PendingSearch = null;
        await WhileBrowseBusyAsync(() => Device.NetUsb.SearchAsync(CurrentInput, item.Index, text, SelectedZoneId));
        await RefreshBrowseAsync();
    }

    private async Task OpenBrowseItemAsync(BrowseItem item)
    {
        if (item.IsSearchable)
        {
            // Needs a search string: show the search box instead of entering the item.
            PendingSearch = item;
            return;
        }

        if (item is { IsContainer: false, IsPlayable: true })
        {
            await PlayBrowseItemAsync(item);
            return;
        }

        // Folders load asynchronously on the device; wait for the new layer before reloading.
        await WhileBrowseBusyAsync(() => Device.NetUsb.EnterFolderAsync(CurrentInput, item.Index, SelectedZoneId));
        await RefreshBrowseAsync();
    }

    private async Task PlayBrowseItemAsync(BrowseItem item)
    {
        await Device.NetUsb.PlayItemAsync(CurrentInput, item.Index, SelectedZoneId);
        await Task.Delay(800);
        await RefreshZoneAsync();
        await RefreshPlayInfoAsync();
    }

    private async Task BrowseBackAsync()
    {
        PendingSearch = null;
        await WhileBrowseBusyAsync(() => Device.NetUsb.ReturnAsync(CurrentInput, SelectedZoneId));
        await RefreshBrowseAsync();
    }

    private async Task WhileBrowseBusyAsync(Func<Task> action)
    {
        BrowseBusy = true;
        try
        {
            await action();
        }
        finally
        {
            BrowseBusy = false;
        }
    }

    private async Task HandleBrowseAndPresetEventAsync(MusicCastEvent e)
    {
        if (IsNetUsb && e.HasFlag("netusb", "list_info_updated"))
        {
            await RefreshBrowseAsync();
        }

        if (e.HasFlag("netusb", "preset_info_updated") || e.HasFlag("netusb", "recent_info_updated") || e.HasFlag("tuner", "preset_info_updated"))
        {
            await RefreshPresetsAsync();
        }
    }
}
