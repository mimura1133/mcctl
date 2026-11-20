using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Mcctl.Models;

namespace Mcctl;

/// <summary>
/// Low level HTTP transport for the Yamaha Extended Control (YXC) API:
/// <c>http://{host}/YamahaExtendedControl/v1/{path}?{query}</c>.
/// </summary>
public sealed class YxcClient : IDisposable
{
    public const string DefaultBasePath = "/YamahaExtendedControl/v1/";

    /// <summary>
    /// One process-wide client so that many devices share the connection pool
    /// (creating an <see cref="HttpClient"/> per device can exhaust sockets).
    /// </summary>
    private static readonly HttpClient SharedHttp = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    private readonly HttpClient _http;

    public YxcClient(string host, HttpClient? httpClient = null, string basePath = DefaultBasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Host = host;
        BaseUri = new Uri($"http://{host}/{basePath.Trim('/')}/");
        _http = httpClient ?? SharedHttp;
    }

    public string Host { get; }

    public Uri BaseUri { get; }

    /// <summary>
    /// When set together with <see cref="EventPort"/>, every request carries
    /// <c>X-AppName</c>/<c>X-AppPort</c> so the device pushes UDP events to this host
    /// for about 10 minutes after the last such request.
    /// The name must start with <c>MusicCast/</c>: the R-N1000A ignores the registration otherwise
    /// (verified: "mcctl/1.0(Windows)" gets no events, "MusicCast/1.0(Windows)" gets them within ~0.2 s).
    /// </summary>
    public string EventAppName { get; set; } = "MusicCast/1.0(Windows)";

    public int? EventPort { get; set; }

    /// <summary>Raised for every completed request (path, raw JSON). Useful for logging.</summary>
    public event Action<string, string>? RawResponse;

    public Task<YxcResponse> GetAsync(string path, Query? query = null, CancellationToken ct = default)
        => GetAsync<YxcResponse>(path, query, ct);

    public async Task<T> GetAsync<T>(string path, Query? query = null, CancellationToken ct = default)
        where T : YxcResponse
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(path, query));
        return await SendAsync<T>(request, path, ct).ConfigureAwait(false);
    }

    public Task<YxcResponse> PostAsync(string path, object body, CancellationToken ct = default)
        => PostAsync<YxcResponse>(path, body, ct);

    public async Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default)
        where T : YxcResponse
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path, null))
        {
            Content = JsonBody(body),
        };
        return await SendAsync<T>(request, path, ct).ConfigureAwait(false);
    }

    /// <summary>UTF-8 JSON with a plain "application/json" content type (no charset parameter), as other YXC clients send it.</summary>
    private static ByteArrayContent JsonBody(object body)
    {
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), YxcJson.Options));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    /// <summary>Turns a relative URL returned by the device (album art etc.) into an absolute one.</summary>
    public Uri? ResolveUrl(string? url) => url switch
    {
        null or "" => null,
        _ when Uri.TryCreate(url, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps) => abs,
        _ => new Uri(new Uri($"http://{Host}/"), url.TrimStart('/')),
    };

    public Task<byte[]> DownloadAsync(Uri uri, CancellationToken ct = default) => _http.GetByteArrayAsync(uri, ct);

    internal Uri BuildUri(string path, Query? query)
        => new(BaseUri, query is { Count: > 0 } ? $"{path.TrimStart('/')}?{query}" : path.TrimStart('/'));

    private async Task<T> SendAsync<T>(HttpRequestMessage request, string path, CancellationToken ct)
        where T : YxcResponse
    {
        if (EventPort is int port)
        {
            request.Headers.TryAddWithoutValidation("X-AppName", EventAppName);
            request.Headers.TryAddWithoutValidation("X-AppPort", port.ToString(CultureInfo.InvariantCulture));
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        RawResponse?.Invoke(path, json);

        var result = JsonSerializer.Deserialize<T>(json, YxcJson.Options)
                     ?? throw new InvalidDataException($"Empty response for '{path}'.");
        return result.ResponseCode == 0 ? result : throw new YxcException(result.ResponseCode, path);
    }

    /// <summary>The shared <see cref="HttpClient"/> is never disposed; injected clients belong to the caller.</summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// Ordered query string builder that formats values the way YXC expects:
/// booleans as <c>true</c>/<c>false</c>, numbers invariant, and enum members in snake_case
/// (<c>PlaybackCommand.PlayPause</c> → <c>play_pause</c>).
/// </summary>
public sealed class Query : List<KeyValuePair<string, string>>
{
    public Query Add(string key, object? value)
    {
        if (value is not null)
        {
            Add(KeyValuePair.Create(key, Format(value)));
        }

        return this;
    }

    public static Query Of(string key, object? value) => new Query().Add(key, value);

    internal static string Format(object value) => value switch
    {
        bool b => b ? "true" : "false",
        Enum e => JsonNamingPolicy.SnakeCaseLower.ConvertName(e.ToString()),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public override string ToString()
        => string.Join('&', this.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
}
