using System.Collections.ObjectModel;
using System.Windows.Threading;
using Mcctl.Api;
using Mcctl.App.Localization;
using Mcctl.App.Mvvm;
using Mcctl.Events;
using Mcctl.Models;

namespace Mcctl.App.ViewModels;

/// <summary>
/// State and commands for one MusicCast device (and its selected zone).
/// Split by feature: Playback / Browse / Presets / Link live in the other partial files.
/// </summary>
public sealed partial class DeviceViewModel : ObservableObject, IDisposable
{
    private readonly Func<IEnumerable<DeviceViewModel>> _allDevices;
    private readonly DispatcherTimer _volumeDebounce;
    private readonly DispatcherTimer _toneDebounce;
    private Dictionary<string, string> _names = [];
    private int _suppressDepth;

    public DeviceViewModel(MusicCastDevice device, Func<IEnumerable<DeviceViewModel>> allDevices)
    {
        Device = device;
        _allDevices = allDevices;

        _volumeDebounce = CreateDebounce(TimeSpan.FromMilliseconds(150), () => ZoneApi.SetVolumeAsync(Volume));
        _toneDebounce = CreateDebounce(TimeSpan.FromMilliseconds(250), () => ZoneApi.SetToneControlAsync(Bass, Treble));

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _progressTimer.Tick += (_, _) => AdvanceProgress();
        _progressTimer.Start();

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public MusicCastDevice Device { get; }

    public string Host => Device.Host;

    // ---------- identity ----------
    public string Name { get; private set => Set(ref field, value); } = "";

    public string Model { get; private set => Set(ref field, value); } = "";

    public bool IsOnline { get; private set => Set(ref field, value); }

    /// <summary>Device details for the Info tab, in the current UI language.</summary>
    public string InfoText => Device.Info is not { } info ? "" : string.Join(Environment.NewLine,
        $"{Loc.Get("InfoModel")}: {info.ModelName}",
        $"{Loc.Get("InfoDeviceId")}: {info.DeviceId}",
        $"{Loc.Get("InfoSystemId")}: {info.SystemId}",
        $"{Loc.Get("InfoDestination")}: {info.Destination}",
        $"{Loc.Get("InfoFirmware")}: {info.SystemVersion}",
        $"{Loc.Get("InfoApiVersion")}: {info.ApiVersion}",
        $"{Loc.Get("InfoNetModule")}: {info.NetmoduleVersion}",
        $"{Loc.Get("InfoIpAddress")}: {Host}");

    // ---------- zone ----------
    public ObservableCollection<OptionItem> Zones { get; } = [];

    public OptionItem? SelectedZone
    {
        get;
        set
        {
            if (Set(ref field, value) && value is not null && !IsSuppressed)
            {
                ApplyZoneFeatures();
                _ = RefreshZoneAsync();
            }
        }
    }

    public string SelectedZoneId => SelectedZone?.Id ?? ZoneIds.Main;

    private ZoneApi ZoneApi => Device.Zone(SelectedZoneId);

    private ZoneFeatures? ZoneFeatures => Device.Features?.FindZone(SelectedZoneId);

    public bool IsOn
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(PowerActionToolTip));
            }
        }
    }

    /// <summary>Power button tooltip: what pressing it will do.</summary>
    public string PowerActionToolTip => Loc.Get(IsOn ? "StandbyAction" : "PowerOnAction");

    public int Volume
    {
        get;
        set
        {
            if (Set(ref field, value) && !IsSuppressed)
            {
                Restart(_volumeDebounce);
            }
        }
    }

    public int MaxVolume { get; private set => Set(ref field, value); } = 100;

    public string VolumeText { get; private set => Set(ref field, value); } = "";

    public bool Mute { get; private set => Set(ref field, value); }

    public ObservableCollection<OptionItem> Inputs { get; } = [];

    public OptionItem? SelectedInput
    {
        get;
        set
        {
            if (Set(ref field, value) && value is not null && !IsSuppressed)
            {
                _ = SendAndRefreshAsync(() => ZoneApi.SetInputAsync(value.Id));
            }
        }
    }

    public string CurrentInput => SelectedInput?.Id ?? "";

    public ObservableCollection<OptionItem> SoundPrograms { get; } = [];

    public OptionItem? SelectedSoundProgram
    {
        get;
        set
        {
            if (Set(ref field, value) && value is not null && !IsSuppressed)
            {
                _ = SendAndRefreshAsync(() => ZoneApi.SetSoundProgramAsync(value.Id));
            }
        }
    }

    public bool HasSoundPrograms => SoundPrograms.Count > 0;

    public IReadOnlyList<SleepOption> SleepOptions { get; private set => Set(ref field, value); } = BuildSleepOptions();

    private static IReadOnlyList<SleepOption> BuildSleepOptions()
        => [.. new[] { 0, 30, 60, 90, 120 }.Select(m => new SleepOption(m, m == 0 ? Loc.Get("SleepOff") : Loc.Format("SleepMinutes", m)))];

    public SleepOption? SelectedSleep
    {
        get;
        set
        {
            if (Set(ref field, value) && value is not null && !IsSuppressed)
            {
                _ = SendAndRefreshAsync(() => ZoneApi.SetSleepAsync(value.Minutes));
            }
        }
    }

    // ---------- sound ----------
    public bool SupportsTone { get; private set => Set(ref field, value); }

    public int Bass
    {
        get;
        set
        {
            if (Set(ref field, value) && !IsSuppressed)
            {
                Restart(_toneDebounce);
            }
        }
    }

    public int Treble
    {
        get;
        set
        {
            if (Set(ref field, value) && !IsSuppressed)
            {
                Restart(_toneDebounce);
            }
        }
    }

    public int ToneMin { get; private set => Set(ref field, value); } = -12;

    public int ToneMax { get; private set => Set(ref field, value); } = 12;

    public ObservableCollection<FeatureToggle> FeatureToggles { get; } = [];

    // ---------- commands ----------
    // Created on first use (C# 14 field keyword) so each partial file declares its commands next to its logic.
    public AsyncCommand PowerCommand => field ??= new AsyncCommand(TogglePowerAsync);
    public AsyncCommand MuteCommand => field ??= new AsyncCommand(() => SendAndRefreshAsync(() => ZoneApi.SetMuteAsync(!Mute)));
    public AsyncCommand VolumeUpCommand => field ??= new AsyncCommand(() => SendAndRefreshAsync(() => ZoneApi.VolumeUpAsync()));
    public AsyncCommand VolumeDownCommand => field ??= new AsyncCommand(() => SendAndRefreshAsync(() => ZoneApi.VolumeDownAsync()));
    public AsyncCommand ToggleFeatureCommand => field ??= new AsyncCommand<FeatureToggle>(t => SendAndRefreshAsync(() => t.Feature.Write(ZoneApi, t.State)));
    public AsyncCommand RefreshCommand => field ??= new AsyncCommand(RefreshAllAsync);

    // =====================================================================
    // Loading
    // =====================================================================

    public async Task InitializeAsync()
    {
        await Device.InitializeAsync();
        IsOnline = true;
        Name = Device.DisplayName;
        Model = Device.Info!.ModelName;
        OnPropertyChanged(nameof(InfoText));

        try
        {
            var names = await Device.System.GetNameTextAsync();
            _names = new[] { names.ZoneList, names.InputList, names.SoundProgramList }
                .SelectMany(list => list ?? [])
                .DistinctBy(x => x.Id)
                .ToDictionary(x => x.Id, x => x.Text);
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            // Names are cosmetic; fall back to built-in labels.
        }

        using (SuppressUpdates())
        {
            ReplaceAll(Zones, Device.ZoneIds.Select(ZoneOption));
            SelectedZone = Zones.FirstOrDefault();
        }

        InitializeTuner();
        ApplyZoneFeatures();
        try
        {
            await RefreshAllAsync();
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            // A partial refresh failure must not hide the device from the list.
            AsyncCommand.Report(ex);
        }
    }

    private void ApplyZoneFeatures()
    {
        var zf = ZoneFeatures;
        using var _ = SuppressUpdates();

        ReplaceAll(Inputs, (zf?.InputList ?? [])
            .Select(id => new OptionItem(id, DisplayNames.Get(id, _names)))
            .OrderBy(i => i.Name, DisplayNames.NaturalOrder));
        ReplaceAll(SoundPrograms, (zf?.SoundProgramList ?? []).Select(id => new OptionItem(id, DisplayNames.Get(id, _names))));
        OnPropertyChanged(nameof(HasSoundPrograms));

        MaxVolume = (int?)zf?.Range("volume")?.Max ?? 100;
        SupportsTone = zf?.Supports("tone_control") == true;
        if (zf?.Range("tone_control") is { } tone)
        {
            ToneMin = (int)tone.Min;
            ToneMax = (int)tone.Max;
        }

        ReplaceAll(FeatureToggles, SoundFeature.All.Where(f => zf?.Supports(f.Func) == true).Select(f => new FeatureToggle(f)));
    }

    public async Task RefreshAllAsync()
    {
        await RefreshZoneAsync();
        await RefreshPlayInfoAsync();
        await RefreshPresetsAsync();
        await RefreshLinkAsync();
    }

    public async Task RefreshZoneAsync()
    {
        ZoneStatus st;
        try
        {
            st = await ZoneApi.GetStatusAsync();
            IsOnline = true;
        }
        catch (Exception ex) when (ex.IsConnectionError)
        {
            IsOnline = false;
            return;
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            AsyncCommand.Report(ex);
            return;
        }

        using (SuppressUpdates())
        {
            IsOn = st.IsOn;
            if (!_volumeDebounce.IsEnabled)
            {
                Volume = st.Volume;
            }

            if (st.MaxVolume > 0)
            {
                MaxVolume = st.MaxVolume;
            }

            Mute = st.Mute;
            VolumeText = st.ActualVolume is { Unit: "dB" } av ? $"{av.Value:0.0} dB" : st.Volume.ToString();
            if (!string.IsNullOrEmpty(st.Input) && !Inputs.Any(i => i.Id == st.Input))
            {
                InsertSorted(Inputs, new OptionItem(st.Input, DisplayNames.Get(st.Input, _names)));
            }

            SelectedInput = Inputs.FirstOrDefault(i => i.Id == st.Input);
            SelectedSoundProgram = SoundPrograms.FirstOrDefault(p => p.Id == st.SoundProgram);
            SelectedSleep = SleepOptions.FirstOrDefault(s => s.Minutes == st.Sleep) ?? SleepOptions[0];
            if (st.ToneControl is { } tc && !_toneDebounce.IsEnabled)
            {
                Bass = tc.Bass;
                Treble = tc.Treble;
            }

            foreach (var toggle in FeatureToggles)
            {
                toggle.State = toggle.Feature.Read(st) ?? false;
            }
        }

        var newType = Device.GetPlayInfoType(st.Input);
        var typeChanged = newType != PlayInfoType;
        PlayInfoType = newType;
        if (typeChanged)
        {
            await RefreshPlayInfoAsync();
        }

        // Reload the browse list whenever the source changes (e.g. Server -> radiko), not only
        // when the play-info family changes.
        if (st.Input != _browseInput)
        {
            await RefreshBrowseAsync();
        }
    }

    // =====================================================================
    // Actions
    // =====================================================================

    private async Task TogglePowerAsync()
    {
        await ZoneApi.SetPowerAsync(IsOn ? PowerState.Standby : PowerState.On);
        await Task.Delay(300);
        await RefreshZoneAsync();
    }

    /// <summary>Sends a zone command and re-reads the zone so the UI reflects what the device accepted.</summary>
    private async Task SendAndRefreshAsync(Func<Task> send)
    {
        await send();
        await Task.Delay(150);
        await RefreshZoneAsync();
    }

    // =====================================================================
    // Events
    // =====================================================================

    /// <summary>Applies a UDP push event (must be called on the UI thread).</summary>
    public async Task HandleEventAsync(MusicCastEvent e)
    {
        try
        {
            await HandleZoneEventAsync(e);
            await HandlePlaybackEventAsync(e);
            await HandleBrowseAndPresetEventAsync(e);
            if (e.HasFlag("dist", "dist_info_updated"))
            {
                await RefreshLinkAsync();
            }
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            // The next event or the fallback poll will catch up.
        }
    }

    private async Task HandleZoneEventAsync(MusicCastEvent e)
    {
        var zone = SelectedZoneId;
        if (!e.HasSection(zone))
        {
            return;
        }

        if (e.TryGetInt(zone, "volume", out var volume) && !_volumeDebounce.IsEnabled)
        {
            using (SuppressUpdates())
            {
                Volume = volume;
                VolumeText = volume.ToString();
            }
        }

        if (e.TryGetBool(zone, "mute", out var mute))
        {
            Mute = mute;
        }

        if (e.TryGetString(zone, "power", out var power))
        {
            IsOn = power == "on";
        }

        if (e.HasFlag(zone, "status_updated") || e.TryGetString(zone, "input", out _))
        {
            await RefreshZoneAsync();
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private bool IsSuppressed => _suppressDepth > 0;

    /// <summary>
    /// While the returned scope is alive, property setters only update the UI and do not send
    /// commands back to the device (used when applying state read from the device).
    /// </summary>
    private SuppressScope SuppressUpdates()
    {
        _suppressDepth++;
        return new SuppressScope(this);
    }

    private readonly struct SuppressScope(DeviceViewModel owner) : IDisposable
    {
        public void Dispose() => owner._suppressDepth--;
    }

    private static DispatcherTimer CreateDebounce(TimeSpan delay, Func<Task> send)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                await send();
            }
            catch (Exception ex)
            {
                AsyncCommand.Report(ex);
            }
        };
        return timer;
    }

    private static void Restart(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    /// <summary>Inserts an option keeping the list in <see cref="DisplayNames.NaturalOrder"/>.</summary>
    private static void InsertSorted(ObservableCollection<OptionItem> target, OptionItem item)
    {
        var index = target.TakeWhile(existing => DisplayNames.NaturalOrder.Compare(existing.Name, item.Name) <= 0).Count();
        target.Insert(index, item);
    }

    private static void ReplaceAll<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private OptionItem ZoneOption(string zone) => new(zone, zone == ZoneIds.Main ? Loc.Get("MainZone") : DisplayNames.Get(zone, _names));

    /// <summary>Re-creates texts built in code; XAML labels follow the language on their own.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        using (SuppressUpdates())
        {
            var zone = SelectedZoneId;
            ReplaceAll(Zones, Device.ZoneIds.Select(ZoneOption));
            SelectedZone = Zones.FirstOrDefault(z => z.Id == zone);

            var sleep = SelectedSleep?.Minutes ?? 0;
            SleepOptions = BuildSleepOptions();
            SelectedSleep = SleepOptions.FirstOrDefault(s => s.Minutes == sleep);
        }

        OnPropertiesChanged(nameof(InfoText), nameof(PowerActionToolTip), nameof(LinkRoleText));
    }

    public override string ToString() => Name;

    public void Dispose()
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _progressTimer.Stop();
        _volumeDebounce.Stop();
        _toneDebounce.Stop();
        Device.Dispose();
    }
}
