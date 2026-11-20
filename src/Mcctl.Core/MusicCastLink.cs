using Mcctl.Models;

namespace Mcctl;

/// <summary>
/// MusicCast Link (multi-room) helpers. The flow mirrors the official controller:
/// <list type="number">
/// <item>each client: <c>dist/setClientInfo</c> {group_id, zone, server_ip_address}</item>
/// <item>server: <c>dist/setServerInfo</c> {group_id, zone, type:"add", client_list}</item>
/// <item>server: <c>dist/startDistribution?num=0</c></item>
/// </list>
/// Unlinking reverses it with an empty group id / type "remove".
/// </summary>
public static class MusicCastLink
{
    /// <summary>Creates a new random group id (32 hex characters).</summary>
    public static string NewGroupId() => Guid.NewGuid().ToString("N");

    /// <summary>Adds <paramref name="clients"/> to the group served by <paramref name="server"/> (creating it if needed).</summary>
    /// <returns>The group id in use.</returns>
    public static async Task<string> LinkAsync(
        MusicCastDevice server,
        IEnumerable<MusicCastDevice> clients,
        string serverZone = ZoneIds.Main,
        string clientZone = ZoneIds.Main,
        string? groupName = null,
        CancellationToken ct = default)
    {
        var clientList = clients.Where(c => c.Host != server.Host).ToList();
        var dist = await server.Distribution.GetDistributionInfoAsync(ct).ConfigureAwait(false);
        var groupId = dist.Role == "server" && dist.HasGroup ? dist.GroupId : NewGroupId();

        foreach (var client in clientList)
        {
            // A client that is a server of another group must dissolve it first.
            var cdist = await client.Distribution.GetDistributionInfoAsync(ct).ConfigureAwait(false);
            if (cdist.Role == "server" && cdist.HasGroup && cdist.GroupId != groupId)
            {
                await DissolveAsync(client, [], ct).ConfigureAwait(false);
            }

            await client.Distribution.SetClientInfoAsync(groupId, [clientZone], server.Host, ct).ConfigureAwait(false);
        }

        await server.Distribution.SetServerInfoAsync(groupId, serverZone, "add", clientList.Select(c => c.Host), ct).ConfigureAwait(false);
        await server.Distribution.StartDistributionAsync(0, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(groupName))
        {
            try
            {
                await server.Distribution.SetGroupNameAsync(groupName, ct).ConfigureAwait(false);
            }
            catch (YxcException)
            {
                // Optional, not supported by every model.
            }
        }

        // Make sure the clients play the linked stream and are powered on.
        foreach (var client in clientList)
        {
            try
            {
                await client.Zone(clientZone).SetPowerAsync(Api.PowerState.On, ct).ConfigureAwait(false);
                var st = await client.Zone(clientZone).GetStatusAsync(ct).ConfigureAwait(false);
                if (st.Input != "mc_link")
                {
                    await client.Zone(clientZone).SetInputAsync("mc_link", ct: ct).ConfigureAwait(false);
                }
            }
            catch (YxcException)
            {
            }
        }

        return groupId;
    }

    /// <summary>Removes <paramref name="clients"/> from the group of <paramref name="server"/>. Dissolves it if none remain.</summary>
    public static async Task UnlinkAsync(
        MusicCastDevice server,
        IEnumerable<MusicCastDevice> clients,
        string clientZone = ZoneIds.Main,
        CancellationToken ct = default)
    {
        var clientList = clients.ToList();
        var dist = await server.Distribution.GetDistributionInfoAsync(ct).ConfigureAwait(false);

        foreach (var client in clientList)
        {
            await client.Distribution.SetClientInfoAsync("", [clientZone], null, ct).ConfigureAwait(false);
        }

        if (dist.Role != "server")
        {
            return;
        }

        var removed = clientList.Select(c => c.Host).ToHashSet();
        var remaining = dist.ClientList.Where(c => !removed.Contains(c.IpAddress)).ToList();
        if (remaining.Count == 0)
        {
            await server.Distribution.StopDistributionAsync(ct).ConfigureAwait(false);
            await server.Distribution.SetServerInfoAsync("", ct: ct).ConfigureAwait(false);
        }
        else
        {
            await server.Distribution.SetServerInfoAsync(dist.GroupId, dist.ServerZone ?? ZoneIds.Main, "remove", removed, ct).ConfigureAwait(false);
            await server.Distribution.StartDistributionAsync(0, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Dissolves the whole group served by <paramref name="server"/>.</summary>
    /// <param name="knownDevices">Devices that might be clients (used to reset their client state).</param>
    public static async Task DissolveAsync(MusicCastDevice server, IEnumerable<MusicCastDevice> knownDevices, CancellationToken ct = default)
    {
        var dist = await server.Distribution.GetDistributionInfoAsync(ct).ConfigureAwait(false);
        var clientIps = dist.ClientList.Select(c => c.IpAddress).ToHashSet();
        foreach (var client in knownDevices.Where(d => clientIps.Contains(d.Host)))
        {
            try
            {
                await client.Distribution.SetClientInfoAsync("", [ZoneIds.Main], null, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsDeviceError)
            {
            }
        }

        await server.Distribution.StopDistributionAsync(ct).ConfigureAwait(false);
        await server.Distribution.SetServerInfoAsync("", ct: ct).ConfigureAwait(false);
    }

    /// <summary>A client leaves whatever group it is part of.</summary>
    public static async Task LeaveAsync(MusicCastDevice client, IEnumerable<MusicCastDevice> knownDevices, string clientZone = ZoneIds.Main, CancellationToken ct = default)
    {
        foreach (var candidate in knownDevices.Where(d => d.Host != client.Host))
        {
            DistributionInfo dist;
            try
            {
                dist = await candidate.Distribution.GetDistributionInfoAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsDeviceError)
            {
                continue;
            }

            if (dist.Role == "server" && dist.ClientList.Any(c => c.IpAddress == client.Host))
            {
                await UnlinkAsync(candidate, [client], clientZone, ct).ConfigureAwait(false);
                return;
            }
        }

        await client.Distribution.SetClientInfoAsync("", [clientZone], null, ct).ConfigureAwait(false);
    }
}
