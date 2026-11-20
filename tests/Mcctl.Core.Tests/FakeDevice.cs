using System.Net;
using System.Text;

namespace Mcctl.Core.Tests;

/// <summary>In-memory HTTP handler emulating one or more YXC devices.</summary>
internal sealed class FakeDevice : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, string>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Host, string Method, string PathAndQuery, string? Body, HttpRequestHeaders Headers)> Requests { get; } = [];

    /// <summary>Content-Type of each request body (null for GET), in request order.</summary>
    public List<string?> ContentTypes { get; } = [];

    public FakeDevice On(string host, string path, string json) => On(host, path, _ => json);

    public FakeDevice On(string host, string path, Func<HttpRequestMessage, string> responder)
    {
        _routes[$"{host}/{path}"] = responder;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath.Replace("/YamahaExtendedControl/v1/", "");
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((uri.Host, request.Method.Method, path + uri.Query, body, new HttpRequestHeaders(request)));
        ContentTypes.Add(request.Content?.Headers.ContentType?.ToString());

        var json = _routes.TryGetValue($"{uri.Host}/{path}", out var r) ? r(request) : "{\"response_code\":0}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    internal sealed class HttpRequestHeaders(HttpRequestMessage m)
    {
        public string? Get(string name) => m.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;
    }
}
