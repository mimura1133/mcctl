using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>system/*</c> endpoints.</summary>
public sealed class SystemApi(YxcClient client)
{
    public Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
        => client.GetAsync<DeviceInfo>("system/getDeviceInfo", null, ct);

    public Task<Features> GetFeaturesAsync(CancellationToken ct = default)
        => client.GetAsync<Features>("system/getFeatures", null, ct);

    public Task<NetworkStatus> GetNetworkStatusAsync(CancellationToken ct = default)
        => client.GetAsync<NetworkStatus>("system/getNetworkStatus", null, ct);

    public Task<FuncStatus> GetFuncStatusAsync(CancellationToken ct = default)
        => client.GetAsync<FuncStatus>("system/getFuncStatus", null, ct);

    public Task<LocationInfo> GetLocationInfoAsync(CancellationToken ct = default)
        => client.GetAsync<LocationInfo>("system/getLocationInfo", null, ct);

    /// <param name="id">Zone id, input id or sound program id. Null returns every name.</param>
    public Task<NameText> GetNameTextAsync(string? id = null, CancellationToken ct = default)
        => client.GetAsync<NameText>("system/getNameText", id is null ? null : Query.Of("id", id), ct);

    public Task<YxcResponse> SetAutoPowerStandbyAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync("system/setAutoPowerStandby", Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetDimmerAsync(int value, CancellationToken ct = default)
        => client.GetAsync("system/setDimmer", Query.Of("value", value), ct);

    public Task<YxcResponse> SetNameTextAsync(string id, string text, CancellationToken ct = default)
        => client.PostAsync("system/setNameText", new { id, text }, ct);

    /// <summary>Sends any request with the event headers to (re)subscribe to UDP events.</summary>
    public Task<YxcResponse> KeepEventAliveAsync(CancellationToken ct = default)
        => client.GetAsync("system/getDeviceInfo", null, ct);
}
