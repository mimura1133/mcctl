using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>dist/*</c> endpoints used for MusicCast Link (multi-room groups).</summary>
public sealed class DistributionApi(YxcClient client)
{
    public Task<DistributionInfo> GetDistributionInfoAsync(CancellationToken ct = default)
        => client.GetAsync<DistributionInfo>("dist/getDistributionInfo", null, ct);

    /// <summary>Configures this device as the server (master) of a group.</summary>
    /// <param name="groupId">32 hex chars; empty string clears the server role.</param>
    /// <param name="type">"add" or "remove" for <paramref name="clientIps"/>.</param>
    public Task<YxcResponse> SetServerInfoAsync(string groupId, string? zone = null, string? type = null, IEnumerable<string>? clientIps = null, CancellationToken ct = default)
        => client.PostAsync("dist/setServerInfo", new ServerInfoBody
        {
            GroupId = groupId,
            Zone = zone,
            Type = type,
            ClientList = clientIps?.ToList(),
        }, ct);

    /// <summary>Configures this device as a client of <paramref name="serverIp"/>.</summary>
    /// <param name="groupId">Empty string leaves the group.</param>
    public Task<YxcResponse> SetClientInfoAsync(string groupId, IEnumerable<string> zones, string? serverIp = null, CancellationToken ct = default)
        => client.PostAsync("dist/setClientInfo", new ClientInfoBody
        {
            GroupId = groupId,
            Zone = zones.ToList(),
            ServerIpAddress = serverIp,
        }, ct);

    /// <param name="num">Arbitrary number, 0 is used by the official app.</param>
    public Task<YxcResponse> StartDistributionAsync(int num = 0, CancellationToken ct = default)
        => client.GetAsync("dist/startDistribution", Query.Of("num", num), ct);

    public Task<YxcResponse> StopDistributionAsync(CancellationToken ct = default)
        => client.GetAsync("dist/stopDistribution", null, ct);

    public Task<YxcResponse> SetGroupNameAsync(string name, CancellationToken ct = default)
        => client.PostAsync("dist/setGroupName", new { name }, ct);

    private sealed class ServerInfoBody
    {
        public string GroupId { get; set; } = "";
        public string? Zone { get; set; }
        public string? Type { get; set; }
        public List<string>? ClientList { get; set; }
    }

    private sealed class ClientInfoBody
    {
        public string GroupId { get; set; } = "";
        public List<string> Zone { get; set; } = [];
        public string? ServerIpAddress { get; set; }
    }
}
