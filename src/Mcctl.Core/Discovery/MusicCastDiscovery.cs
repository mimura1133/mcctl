using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace Mcctl.Discovery;

/// <summary>A device found by SSDP.</summary>
public sealed record DiscoveredDevice(
    string Host,
    string FriendlyName,
    string ModelName,
    string? SerialNumber,
    string? Udn,
    Uri Location,
    Uri YxcBaseUri)
{
    public override string ToString() => $"{FriendlyName} [{ModelName}] {Host}";
}

/// <summary>
/// Finds MusicCast devices with SSDP (UPnP M-SEARCH). A device is MusicCast capable when its
/// device description contains <c>yamaha:X_yxcControlURL</c> (namespace <c>urn:schemas-yamaha-com:device-1-0</c>).
/// </summary>
public static class MusicCastDiscovery
{
    private static readonly IPEndPoint SsdpEndpoint = new(IPAddress.Parse("239.255.255.250"), 1900);
    private static readonly XNamespace Upnp = "urn:schemas-upnp-org:device-1-0";
    private static readonly XNamespace Yamaha = "urn:schemas-yamaha-com:device-1-0";

    private static readonly string[] SearchTargets =
    [
        "urn:schemas-upnp-org:device:MediaRenderer:1",
        "upnp:rootdevice",
    ];

    private static readonly HttpClient DescriptionHttp = new() { Timeout = TimeSpan.FromSeconds(4) };

    /// <summary>Searches every active IPv4 interface and returns distinct devices.</summary>
    public static async Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(
        TimeSpan? timeout = null,
        HttpClient? httpClient = null,
        IProgress<DiscoveredDevice>? progress = null,
        CancellationToken ct = default,
        Action<string>? log = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(3);
        var http = httpClient ?? DescriptionHttp;

        // Each interface collects its own replies; merge afterwards instead of sharing a locked set.
        var perInterface = await Task.WhenAll(GetLocalAddresses()
            .Select(addr => SearchOnInterfaceAsync(addr, wait, log, ct))).ConfigureAwait(false);
        var locations = perInterface.SelectMany(l => l).Distinct(StringComparer.OrdinalIgnoreCase);

        var devices = await Task.WhenAll(locations.Select(async loc =>
        {
            var dev = await TryReadDescriptionAsync(http, new Uri(loc), log, ct).ConfigureAwait(false);
            if (dev is not null)
            {
                progress?.Report(dev);
            }

            return dev;
        })).ConfigureAwait(false);

        return devices
            .OfType<DiscoveredDevice>()
            .DistinctBy(d => d.Host)
            .OrderBy(d => d.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<IPAddress> GetLocalAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic is { OperationalStatus: OperationalStatus.Up, SupportsMulticast: true }
                          && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(ua => ua.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .ToList();
        return addresses is [] ? [IPAddress.Any] : addresses;
    }

    /// <returns>LOCATION URLs of every SSDP reply received on <paramref name="local"/>.</returns>
    private static async Task<IReadOnlyCollection<string>> SearchOnInterfaceAsync(IPAddress local, TimeSpan wait, Action<string>? log, CancellationToken ct)
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(local, 0));
            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);
            if (!local.Equals(IPAddress.Any))
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
            }

            if (OperatingSystem.IsWindows())
            {
                // Ignore ICMP "port unreachable" (WSAECONNRESET) so one bad reply cannot end the receive loop.
                const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
                udp.Client.IOControl(SIO_UDP_CONNRESET, [0, 0, 0, 0], null);
            }
        }
        catch (SocketException ex)
        {
            log?.Invoke($"[if {local}] bind failed: {ex.Message}");
            return locations;
        }

        log?.Invoke($"[if {local}] M-SEARCH from {udp.Client.LocalEndPoint}");
        var mx = Math.Clamp((int)wait.TotalSeconds - 1, 1, 5);
        try
        {
            foreach (var st in SearchTargets)
            {
                var msg = Encoding.ASCII.GetBytes(
                    $"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: {mx}\r\nST: {st}\r\n\r\n");

                // UDP is lossy; send twice.
                await udp.SendAsync(msg, SsdpEndpoint, ct).ConfigureAwait(false);
                await udp.SendAsync(msg, SsdpEndpoint, ct).ConfigureAwait(false);
            }
        }
        catch (SocketException ex)
        {
            log?.Invoke($"[if {local}] send failed: {ex.Message}");
            return locations;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(wait);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
                var location = ParseLocation(Encoding.UTF8.GetString(result.Buffer));
                log?.Invoke($"[if {local}] reply from {result.RemoteEndPoint}: {location ?? "(no LOCATION)"}");
                if (location is not null)
                {
                    locations.Add(location);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                log?.Invoke($"[if {local}] receive error (ignored): {ex.SocketErrorCode}");
            }
        }

        return locations;
    }

    internal static string? ParseLocation(string ssdpResponse)
    {
        foreach (var line in ssdpResponse.Split("\r\n"))
        {
            if (line.Split(':', 2, StringSplitOptions.TrimEntries) is [var name, var value]
                && name.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<DiscoveredDevice?> TryReadDescriptionAsync(HttpClient http, Uri location, Action<string>? log, CancellationToken ct)
    {
        try
        {
            var xml = await http.GetStringAsync(location, ct).ConfigureAwait(false);
            var dev = ParseDescription(xml, location);
            log?.Invoke(dev is null ? $"[desc] {location}: not a MusicCast device" : $"[desc] {location}: {dev}");
            return dev;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            log?.Invoke($"[desc] {location}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Parses a UPnP device description. Returns null when it is not a MusicCast device.</summary>
    public static DiscoveredDevice? ParseDescription(string xml, Uri location)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null)
        {
            return null;
        }

        var yxcControl = root.Descendants(Yamaha + "X_yxcControlURL").FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(yxcControl))
        {
            return null;
        }

        var urlBase = root.Descendants(Yamaha + "X_URLBase").FirstOrDefault()?.Value;
        var baseUri = !string.IsNullOrWhiteSpace(urlBase) && Uri.TryCreate(urlBase, UriKind.Absolute, out var b)
            ? b
            : new Uri($"http://{location.Host}/");
        var yxcBase = new Uri(baseUri, yxcControl);

        var device = root.Element(Upnp + "device");
        string Get(string name) => device?.Element(Upnp + name)?.Value.Trim() ?? "";

        return new DiscoveredDevice(
            Host: yxcBase.Host,
            FriendlyName: Get("friendlyName") is { Length: > 0 } fn ? fn : yxcBase.Host,
            ModelName: Get("modelName"),
            SerialNumber: Get("serialNumber") is { Length: > 0 } sn ? sn : null,
            Udn: Get("UDN") is { Length: > 0 } udn ? udn : null,
            Location: location,
            YxcBaseUri: yxcBase);
    }
}
