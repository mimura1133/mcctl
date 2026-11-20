using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Mcctl.Events;

/// <summary>
/// One UDP notification pushed by a device. The payload looks like
/// <c>{"device_id":"...","main":{"volume":42},"netusb":{"play_time":12,"play_info_updated":true}}</c>.
/// </summary>
public sealed class MusicCastEvent(IPAddress source, JsonElement root, string raw)
{
    public IPAddress Source { get; } = source;

    public string Raw { get; } = raw;

    public JsonElement Root { get; } = root;

    public string? DeviceId => Root.TryGetProperty("device_id", out var v) ? v.GetString() : null;

    /// <summary>Section of the event: "system", "main", "zone2", "netusb", "tuner", "cd", "dist", "clock".</summary>
    public bool HasSection(string section) => Root.TryGetProperty(section, out var s) && s.ValueKind == JsonValueKind.Object;

    public IEnumerable<string> Sections =>
        Root.ValueKind == JsonValueKind.Object
            ? Root.EnumerateObject().Select(p => p.Name).Where(n => n != "device_id")
            : [];

    /// <summary>True when the device signals that <paramref name="section"/>/<paramref name="flag"/> changed (e.g. "main", "status_updated").</summary>
    public bool HasFlag(string section, string flag)
        => TryGetValue(section, flag, out var f) && f.ValueKind == JsonValueKind.True;

    public bool TryGetInt(string section, string property, out int value)
    {
        value = 0;
        return TryGetValue(section, property, out var p) && p.TryGetInt32(out value);
    }

    public bool TryGetBool(string section, string property, out bool value)
    {
        var found = TryGetValue(section, property, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False;
        value = found && p.GetBoolean();
        return found;
    }

    public bool TryGetString(string section, string property, out string value)
    {
        var found = TryGetValue(section, property, out var p) && p.ValueKind == JsonValueKind.String;
        value = found ? p.GetString() ?? "" : "";
        return found;
    }

    /// <summary>Reads <c>{section}.{property}</c>.</summary>
    public bool TryGetValue(string section, string property, out JsonElement value)
    {
        value = default;
        return Root.TryGetProperty(section, out var s) && s.ValueKind == JsonValueKind.Object && s.TryGetProperty(property, out value);
    }

    public override string ToString() => $"{Source}: {Raw}";
}


/// <summary>Receives MusicCast UDP event notifications on a local port.</summary>
public sealed class MusicCastEventListener : IAsyncDisposable, IDisposable
{
    public const int DefaultPort = 41100;

    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    /// <param name="port">Local UDP port. 0 picks a free port (see <see cref="Port"/>).</param>
    public MusicCastEventListener(int port = DefaultPort)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
    }

    public int Port { get; }

    public event EventHandler<MusicCastEvent>? EventReceived;

    public event EventHandler<Exception>? Error;

    public void Start() => _loop ??= Task.Run(() => LoopAsync(_cts.Token));

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                Error?.Invoke(this, ex);
                continue;
            }

            var raw = Encoding.UTF8.GetString(result.Buffer);
            var ev = Parse(result.RemoteEndPoint.Address, raw);
            if (ev is null)
            {
                continue;
            }

            try
            {
                EventReceived?.Invoke(this, ev);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }
    }

    internal static MusicCastEvent? Parse(IPAddress source, string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return new MusicCastEvent(source, doc.RootElement.Clone(), raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _udp.Dispose();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
