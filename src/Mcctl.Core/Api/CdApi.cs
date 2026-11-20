using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>cd/*</c> endpoints.</summary>
public sealed class CdApi(YxcClient client)
{
    public Task<CdPlayInfo> GetPlayInfoAsync(CancellationToken ct = default)
        => client.GetAsync<CdPlayInfo>("cd/getPlayInfo", null, ct);

    public Task<YxcResponse> SetPlaybackAsync(PlaybackCommand command, CancellationToken ct = default)
        => client.GetAsync("cd/setPlayback", Query.Of("playback", command), ct);

    /// <summary>Plays a specific track (1-based).</summary>
    public Task<YxcResponse> PlayTrackAsync(int track, CancellationToken ct = default)
        => client.GetAsync("cd/setPlayback", new Query().Add("playback", "track_select").Add("num", track), ct);

    public Task<YxcResponse> ToggleTrayAsync(CancellationToken ct = default)
        => client.GetAsync("cd/toggleTray", null, ct);

    public Task<YxcResponse> ToggleRepeatAsync(CancellationToken ct = default)
        => client.GetAsync("cd/toggleRepeat", null, ct);

    public Task<YxcResponse> ToggleShuffleAsync(CancellationToken ct = default)
        => client.GetAsync("cd/toggleShuffle", null, ct);
}
