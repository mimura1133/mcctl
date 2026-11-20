using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mcctl.Api;
using Mcctl.App.Localization;
using Mcctl.App.Mvvm;
using Mcctl.Events;
using Mcctl.Models;

namespace Mcctl.App.ViewModels;

/// <summary>Now playing, transport, tuner and CD.</summary>
public sealed partial class DeviceViewModel
{
    private readonly DispatcherTimer _progressTimer;
    private string? _lastAlbumArtUrl;

    public string PlayInfoType
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertiesChanged(nameof(IsNetUsb), nameof(IsTuner), nameof(IsCd), nameof(HasTransport), nameof(ShowsPlayTime));
            }
        }
    } = PlayInfoTypes.None;

    public bool IsNetUsb => PlayInfoType == PlayInfoTypes.NetUsb;
    public bool IsTuner => PlayInfoType == PlayInfoTypes.Tuner;
    public bool IsCd => PlayInfoType == PlayInfoTypes.Cd;
    public bool HasTransport => IsNetUsb || IsCd;

    public string Track { get; private set => Set(ref field, value); } = "";

    public string Artist { get; private set => Set(ref field, value); } = "";

    public string Album { get; private set => Set(ref field, value); } = "";

    public BitmapImage? AlbumArt { get; private set => Set(ref field, value); }

    public string Playback
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(PlayPauseGlyph));
            }
        }
    } = "stop";

    public string PlayPauseGlyph => Playback == "play" ? "" : "";

    public int PlayTime
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertiesChanged(nameof(PlayTimeText), nameof(ShowsPlayTime));
            }
        }
    }

    public int TotalTime
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertiesChanged(nameof(TotalTimeText), nameof(HasDuration));
            }
        }
    }

    public string PlayTimeText => DisplayNames.FormatTime(PlayTime);
    public string TotalTimeText => HasDuration ? DisplayNames.FormatTime(TotalTime) : "";
    public bool HasDuration => TotalTime > 0;

    /// <summary>Elapsed time is meaningful (negative values mean "unknown", e.g. Bluetooth reports -60000).</summary>
    public bool ShowsPlayTime => HasTransport && PlayTime >= 0;

    /// <summary>Decoded audio format of the zone, e.g. "MP3 · 44.1 kHz"; empty when unknown.</summary>
    public string AudioFormatText { get; private set => Set(ref field, value); } = "";

    public string Repeat
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(RepeatGlyph));
            }
        }
    } = "off";

    public string RepeatGlyph => Repeat == "one" ? "" : "";

    public string Shuffle { get; private set => Set(ref field, value); } = "off";

    // ---------- tuner ----------
    public bool HasTuner { get; private set => Set(ref field, value); }

    public string TunerBand { get; private set => Set(ref field, value); } = "fm";

    public string TunerFrequencyText { get; private set => Set(ref field, value); } = "";

    public string TunerStationText { get; private set => Set(ref field, value); } = "";

    public string DirectFrequency { get; set => Set(ref field, value); } = "";

    public ObservableCollection<string> TunerBands { get; } = [];

    // ---------- CD ----------
    public string CdStatus { get; private set => Set(ref field, value); } = "";

    // ---------- commands ----------
    public AsyncCommand PlayPauseCommand => field ??= new AsyncCommand(() => TransportAsync(Playback == "play" ? PlaybackCommand.Pause : PlaybackCommand.Play));
    public AsyncCommand StopCommand => field ??= new AsyncCommand(() => TransportAsync(PlaybackCommand.Stop));
    public AsyncCommand NextCommand => field ??= new AsyncCommand(() => TransportAsync(PlaybackCommand.Next));
    public AsyncCommand PreviousCommand => field ??= new AsyncCommand(() => TransportAsync(PlaybackCommand.Previous));
    public AsyncCommand RepeatCommand => field ??= new AsyncCommand(() => ToggleAndRefreshAsync(IsCd ? Device.Cd.ToggleRepeatAsync : Device.NetUsb.ToggleRepeatAsync));
    public AsyncCommand ShuffleCommand => field ??= new AsyncCommand(() => ToggleAndRefreshAsync(IsCd ? Device.Cd.ToggleShuffleAsync : Device.NetUsb.ToggleShuffleAsync));
    public AsyncCommand TuneUpCommand => field ??= TunerCommand(() => Device.Tuner.TuneAsync(TunerBand, "up"));
    public AsyncCommand TuneDownCommand => field ??= TunerCommand(() => Device.Tuner.TuneAsync(TunerBand, "down"));
    public AsyncCommand SeekUpCommand => field ??= TunerCommand(() => Device.Tuner.TuneAsync(TunerBand, "auto_up"));
    public AsyncCommand SeekDownCommand => field ??= TunerCommand(() => Device.Tuner.TuneAsync(TunerBand, "auto_down"));
    public AsyncCommand SetBandCommand => field ??= new AsyncCommand<string>(band => SendAndRefreshAsync(() => Device.Tuner.SetBandAsync(band)));
    public AsyncCommand DirectTuneCommand => field ??= new AsyncCommand(DirectTuneAsync);
    public AsyncCommand TunerPresetNextCommand => field ??= TunerCommand(() => Device.Tuner.SwitchPresetAsync("next"));
    public AsyncCommand TunerPresetPrevCommand => field ??= TunerCommand(() => Device.Tuner.SwitchPresetAsync("previous"));
    public AsyncCommand CdTrayCommand => field ??= new AsyncCommand(() => SendAndRefreshAsync(() => Device.Cd.ToggleTrayAsync()));

    private AsyncCommand TunerCommand(Func<Task> send) => new(() => TuneAndFollowAsync(send));

    private async Task RefreshAudioFormatAsync()
    {
        try
        {
            var signal = await ZoneApi.GetSignalInfoAsync();
            AudioFormatText = signal.Audio is { Error: 0 } audio ? audio.Describe() : "";
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            AudioFormatText = ""; // not supported by every model
        }
    }

    private void InitializeTuner()
    {
        HasTuner = Device.Features?.Tuner is not null;
        var bands = Device.Features?.Tuner?.FuncList ?? [];
        ReplaceAll(TunerBands, new[] { "am", "fm", "dab" }.Where(bands.Contains));
    }

    /// <summary>Local 1 s tick so the progress bar moves between device events.</summary>
    private void AdvanceProgress()
    {
        if (Playback == "play" && (TotalTime <= 0 || PlayTime < TotalTime))
        {
            PlayTime++;
        }
    }

    public async Task RefreshPlayInfoAsync()
    {
        try
        {
            switch (PlayInfoType)
            {
                case PlayInfoTypes.NetUsb:
                    var net = await Device.NetUsb.GetPlayInfoAsync();
                    ShowNowPlaying(net.Track, net.Artist, net.Album, net.Playback, net.PlayTime, net.TotalTime, net.Repeat, net.Shuffle);
                    await LoadAlbumArtAsync(net.AlbumartUrl);
                    break;
                case PlayInfoTypes.Tuner:
                    await RefreshTunerAsync();
                    break;
                case PlayInfoTypes.Cd:
                    var cd = await Device.Cd.GetPlayInfoAsync();
                    var title = string.IsNullOrEmpty(cd.Track) ? $"Track {cd.TrackNumber}/{cd.TotalTracks}" : cd.Track;
                    ShowNowPlaying(title, cd.Artist, cd.Album, cd.Playback, cd.PlayTime, cd.TotalTime, cd.Repeat, cd.Shuffle);
                    CdStatus = cd.DeviceStatus;
                    await LoadAlbumArtAsync(null);
                    break;
                default:
                    ShowNowPlaying(SelectedInput?.Name ?? "", "", "", "stop", 0, 0);
                    await LoadAlbumArtAsync(null);
                    break;
            }
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            // Keep the last known state; the next event or poll will retry.
        }

        await RefreshAudioFormatAsync();
    }

    private void ShowNowPlaying(string track, string artist, string album, string playback, int playTime, int totalTime,
        string repeat = "off", string shuffle = "off")
    {
        Track = track;
        Artist = artist;
        Album = album;
        Playback = playback;
        PlayTime = playTime;
        TotalTime = totalTime;
        Repeat = repeat;
        Shuffle = shuffle;
    }

    /// <summary>Reads the tuner state, updates the display and returns what the device reported.</summary>
    private async Task<TunerPlayInfo> RefreshTunerAsync()
    {
        var t = await Device.Tuner.GetPlayInfoAsync();
        TunerBand = t.Band;
        if (t is { Band: "dab", Dab: { } dab })
        {
            TunerFrequencyText = $"DAB {dab.ChLabel}";
            TunerStationText = dab.ServiceLabel;
            ShowNowPlaying(dab.ServiceLabel, dab.Dls, dab.EnsembleLabel, "play", 0, 0);
        }
        else
        {
            var band = t.Band == "am" ? t.Am : t.Fm;
            TunerFrequencyText = band is null ? "" : DisplayNames.FormatFrequency(t.Band, band.Freq);
            TunerStationText = t is { Band: "fm", Rds: { } rds } ? $"{rds.ProgramService} {rds.RadioTextA}".Trim() : "";
            if (t.AutoScan)
            {
                // The device keeps reporting the start frequency until a station is found, so say so in the title.
                ShowNowPlaying(Loc.Get("TunerSeeking"), "", "", "play", 0, 0);
            }
            else
            {
                ShowNowPlaying(TunerFrequencyText, TunerStationText, band is { Preset: > 0 } ? $"Preset {band.Preset}" : "", "play", 0, 0);
            }
        }

        await LoadAlbumArtAsync(null);
        return t;
    }

    /// <summary>
    /// Sends a tuner command and refreshes the frequency display right away. During an auto seek the device keeps
    /// reporting the start frequency with <c>auto_scan: true</c> until a station is found (observed on
    /// R-N1000A: about 2–3 s); the title shows "Seeking…" meanwhile and the found frequency as soon as the scan ends.
    /// </summary>
    private async Task TuneAndFollowAsync(Func<Task> send)
    {
        await send();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        do
        {
            await Task.Delay(TunerPollInterval);
        }
        while ((await RefreshTunerAsync()).AutoScan
               && System.Diagnostics.Stopwatch.GetElapsedTime(started) < TunerSeekTimeout);
    }

    private static readonly TimeSpan TunerPollInterval = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan TunerSeekTimeout = TimeSpan.FromSeconds(30);

    private async Task LoadAlbumArtAsync(string? url)
    {
        if (url == _lastAlbumArtUrl)
        {
            return;
        }

        _lastAlbumArtUrl = url;
        try
        {
            AlbumArt = Device.ResolveUrl(url) is { } uri ? ImageLoader.FromBytes(await Device.Client.DownloadAsync(uri)) : null;
        }
        catch (Exception ex) when (ex.IsConnectionError)
        {
            AlbumArt = null;
        }
    }

    private async Task TransportAsync(PlaybackCommand command)
    {
        await (IsCd ? Device.Cd.SetPlaybackAsync(command) : Device.NetUsb.SetPlaybackAsync(command));
        await Task.Delay(300);
        await RefreshPlayInfoAsync();
    }

    private async Task ToggleAndRefreshAsync(Func<CancellationToken, Task<YxcResponse>> toggle)
    {
        await toggle(CancellationToken.None);
        await RefreshPlayInfoAsync();
    }

    private async Task DirectTuneAsync()
    {
        if (!double.TryParse(DirectFrequency, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException(Loc.Get("FrequencyFormatError"));
        }

        var kHz = (int)Math.Round(TunerBand == "fm" ? value * 1000 : value);
        await Device.Tuner.SetFrequencyAsync(TunerBand, kHz);
        await Task.Delay(300);
        await RefreshTunerAsync();
    }

    private async Task HandlePlaybackEventAsync(MusicCastEvent e)
    {
        if (e.TryGetInt("netusb", "play_time", out var netTime) && IsNetUsb)
        {
            PlayTime = netTime;
        }

        if (e.TryGetInt("cd", "play_time", out var cdTime) && IsCd)
        {
            PlayTime = cdTime;
        }

        if ((IsNetUsb && e.HasFlag("netusb", "play_info_updated")) || (IsCd && e.HasFlag("cd", "play_info_updated")))
        {
            await RefreshPlayInfoAsync();
        }

        if (IsTuner && e.HasFlag("tuner", "play_info_updated"))
        {
            await RefreshTunerAsync();
        }

        if (e.HasFlag(SelectedZoneId, "signal_info_updated"))
        {
            await RefreshAudioFormatAsync();
        }
    }
}
