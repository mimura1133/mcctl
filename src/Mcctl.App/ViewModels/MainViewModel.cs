using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Threading;
using Mcctl.App.Localization;
using Mcctl.App.Mvvm;
using Mcctl.Discovery;
using Mcctl.Events;

namespace Mcctl.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly string HostsFile = Path.Combine(AppSettings.Directory, "hosts.json");

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _keepAlive;
    private readonly DispatcherTimer _poll;
    private readonly DispatcherTimer _notificationTimer;
    private readonly List<string> _failedHosts = [];
    private readonly AppSettings _settings = AppSettings.Load();
    private MusicCastEventListener? _listener;

    public MainViewModel()
    {
        Loc.Instance.Language = _settings.Language;
        SelectedLanguage = Languages.First(l => l.Language == _settings.Language);
        AsyncCommand.UnhandledError += ex => _dispatcher.BeginInvoke(() => Notify(DescribeError(ex)));

        // Devices only push events for ~10 minutes after the last request with X-AppPort.
        _keepAlive = new DispatcherTimer { Interval = TimeSpan.FromMinutes(4) };
        _keepAlive.Tick += async (_, _) => await KeepAliveAsync();

        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _notificationTimer.Tick += (_, _) => HideNotification();

        // Fallback refresh in case UDP is blocked by a firewall.
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _poll.Tick += async (_, _) =>
        {
            if (SelectedDevice is { } d)
            {
                await d.RefreshZoneAsync();
                await d.RefreshPlayInfoAsync();
            }
        };
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public DeviceViewModel? SelectedDevice
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                if (value is not null)
                {
                    value.RebuildLinkCandidates();
                    _ = value.RefreshAllAsync();
                }
            }
        }
    }

    public bool HasSelection => SelectedDevice is not null;

    public string ManualHost { get; set => Set(ref field, value); } = "";

    public bool IsSearching { get; private set => Set(ref field, value); }

    // ---------- language ----------

    /// <summary>Language names are shown in their own language so they can be found whatever is selected.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new(AppLanguage.System, "System / システム"),
        new(AppLanguage.Japanese, "日本語"),
        new(AppLanguage.English, "English"),
    ];

    public LanguageOption? SelectedLanguage
    {
        get;
        set
        {
            if (!Set(ref field, value) || value is null || value.Language == Loc.Instance.Language && value.Language == _settings.Language)
            {
                return;
            }

            Loc.Instance.Language = value.Language;
            _settings.Language = value.Language;
            _settings.Save();
        }
    }

    // ---------- layout ----------

    /// <summary>Room pane expanded (true) or collapsed to an icon strip (false).</summary>
    public bool IsRoomPaneOpen { get; set => Set(ref field, value); } = true;

    /// <summary>Mini player layout; set by the window when it gets small.</summary>
    public bool IsCompact { get; set => Set(ref field, value); }

    /// <summary>Keep the window above others (mainly for the mini player).</summary>
    public bool IsPinned { get; set => Set(ref field, value); }

    public RelayCommand ToggleRoomPaneCommand => field ??= new RelayCommand(() => IsRoomPaneOpen = !IsRoomPaneOpen);

    // ---------- notification toast (replaces the status bar) ----------

    public string Notification { get; private set => Set(ref field, value); } = "";

    public bool IsNotificationVisible { get; private set => Set(ref field, value); }

    public RelayCommand DismissNotificationCommand => field ??= new RelayCommand(HideNotification);

    /// <summary>Shows a transient message (errors and results the user should notice).</summary>
    private void Notify(string message)
    {
        Notification = message;
        IsNotificationVisible = true;
        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    private void HideNotification()
    {
        _notificationTimer.Stop();
        IsNotificationVisible = false;
    }

    public AsyncCommand SearchCommand => field ??= new AsyncCommand(SearchAsync, () => !IsSearching);

    public AsyncCommand AddHostCommand => field ??= new AsyncCommand(
        () => AddHostAsync(ManualHost, remember: true), () => !string.IsNullOrWhiteSpace(ManualHost));

    public RelayCommand<DeviceViewModel> RemoveDeviceCommand => field ??= new RelayCommand<DeviceViewModel>(d => _ = RemoveDeviceAsync(d));

    public async Task StartAsync()
    {
        StartEventListener();
        _keepAlive.Start();
        _poll.Start();

        foreach (var host in await LoadHostsAsync())
        {
            await AddHostAsync(host, remember: false);
        }

        await SearchAsync();
    }

    private static string DescribeError(Exception ex) => ex switch
    {
        YxcException y => Loc.Format("ErrorDevice", YxcException.Describe(y.ResponseCode), y.Path),
        HttpRequestException h => Loc.Format("ErrorNetwork", h.Message),
        TaskCanceledException => Loc.Get("ErrorTimeout"),
        _ => Loc.Format("ErrorGeneric", ex.Message),
    };

    private void StartEventListener()
    {
        // Prefer the well-known port; fall back to any free port if another app holds it.
        foreach (var port in (ReadOnlySpan<int>)[MusicCastEventListener.DefaultPort, 0])
        {
            try
            {
                _listener = new MusicCastEventListener(port);
                _listener.EventReceived += OnDeviceEvent;
                _listener.Start();
                return;
            }
            catch (SocketException)
            {
                // Port in use: try the next one.
            }
        }
    }

    private void OnDeviceEvent(object? sender, MusicCastEvent e)
    {
        _dispatcher.BeginInvoke(async () =>
        {
            var host = e.Source.ToString();
            var dev = Devices.FirstOrDefault(d => d.Host == host)
                      ?? Devices.FirstOrDefault(d => e.DeviceId is not null && d.Device.Info?.DeviceId == e.DeviceId);
            if (dev is not null)
            {
                await dev.HandleEventAsync(e);
            }
        });
    }

    private async Task SearchAsync()
    {
        IsSearching = true;
        try
        {
            _failedHosts.Clear();
            var found = await MusicCastDiscovery.DiscoverAsync(TimeSpan.FromSeconds(3));
            foreach (var f in found)
            {
                await AddHostAsync(f.Host, remember: false, f.FriendlyName);
            }

            // Only results the user has to act on are shown; a successful search is visible in the room list.
            if (_failedHosts.Count > 0)
            {
                Notify(Loc.Format("SearchPartialFailure", found.Count, _failedHosts.Count, string.Join(" / ", _failedHosts)));
            }
            else if (Devices.Count == 0)
            {
                Notify(Loc.Get("SearchNoDevices"));
            }
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task AddHostAsync(string host, bool remember, string? friendlyName = null)
    {
        host = host.Trim();
        if (host.Length == 0 || Devices.Any(d => d.Host == host))
        {
            return;
        }

        // The SSDP friendly name is only a fallback; InitializeAsync replaces it with the room name when available.
        var device = new MusicCastDevice(host) { FriendlyName = friendlyName };
        device.Client.EventPort = _listener?.Port;

        var vm = new DeviceViewModel(device, () => Devices);
        try
        {
            await vm.InitializeAsync();
        }
        catch (Exception ex) when (ex.IsDeviceError || ex is UriFormatException)
        {
            vm.Dispose();
            _failedHosts.Add($"{friendlyName ?? host} ({host}): {DescribeError(ex)}");
            if (remember)
            {
                Notify(Loc.Format("CannotConnect", host, DescribeError(ex)));
            }

            return;
        }

        Devices.Add(vm);
        foreach (var d in Devices)
        {
            d.RebuildLinkCandidates();
        }

        SelectedDevice ??= vm;
        if (remember)
        {
            ManualHost = "";
            await SaveHostsAsync();
            Notify(Loc.Format("Added", vm.Name));
        }
    }

    private async Task RemoveDeviceAsync(DeviceViewModel d)
    {
        Devices.Remove(d);
        if (SelectedDevice == d)
        {
            SelectedDevice = Devices.FirstOrDefault();
        }

        d.Dispose();
        await SaveHostsAsync();
    }

    private async Task KeepAliveAsync()
    {
        // Snapshot: the collection may change while awaiting.
        foreach (var d in Devices.ToList())
        {
            try
            {
                await d.Device.System.KeepEventAliveAsync();
            }
            catch (Exception ex) when (ex.IsDeviceError)
            {
                // Offline devices simply stop sending events.
            }
        }
    }

    private static async Task<IReadOnlyList<string>> LoadHostsAsync()
    {
        try
        {
            if (!File.Exists(HostsFile))
            {
                return [];
            }

            await using var stream = File.OpenRead(HostsFile);
            return await JsonSerializer.DeserializeAsync<List<string>>(stream) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task SaveHostsAsync()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Directory);
            await using var stream = File.Create(HostsFile);
            await JsonSerializer.SerializeAsync(stream, Devices.Select(d => d.Host).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the list is best effort.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _keepAlive.Stop();
        _poll.Stop();
        if (_listener is not null)
        {
            await _listener.DisposeAsync();
        }

        foreach (var d in Devices)
        {
            d.Dispose();
        }
    }
}
