using System.Collections.ObjectModel;
using Mcctl.App.Localization;
using Mcctl.App.Mvvm;

namespace Mcctl.App.ViewModels;

/// <summary>MusicCast Link (multi-room groups).</summary>
public sealed partial class DeviceViewModel
{
    public string LinkRole
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                OnPropertyChanged(nameof(LinkRoleText));
            }
        }
    } = "none";

    public string LinkRoleText => LinkRole switch
    {
        "server" => Loc.Get("LinkRoleServer"),
        "client" => Loc.Get("LinkRoleClient"),
        _ => Loc.Get("LinkRoleNone"),
    };

    public string GroupName { get; set => Set(ref field, value); } = "";

    public ObservableCollection<LinkCandidate> LinkCandidates { get; } = [];

    public ObservableCollection<string> LinkedClients { get; } = [];

    public AsyncCommand LinkCommand => field ??= new AsyncCommand(LinkSelectedAsync);
    public AsyncCommand UnlinkCommand => field ??= new AsyncCommand(UnlinkAsync);

    public async Task RefreshLinkAsync()
    {
        try
        {
            var dist = await Device.Distribution.GetDistributionInfoAsync();
            LinkRole = dist.HasGroup ? dist.Role : "none";
            GroupName = dist.GroupName;
            var byHost = _allDevices().ToDictionary(d => d.Host);
            ReplaceAll(LinkedClients, dist.ClientList.Select(c =>
                byHost.TryGetValue(c.IpAddress, out var vm) ? $"{vm.Name} ({c.IpAddress})" : c.IpAddress));
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            LinkRole = "none";
        }

        RebuildLinkCandidates();
    }

    public void RebuildLinkCandidates()
    {
        var checkedHosts = LinkCandidates.Where(c => c.IsChecked).Select(c => c.Device.Host).ToHashSet();
        ReplaceAll(LinkCandidates, _allDevices()
            .Where(d => d != this)
            .Select(d => new LinkCandidate(d) { IsChecked = checkedHosts.Contains(d.Host) }));
    }

    private async Task LinkSelectedAsync()
    {
        var targets = LinkCandidates.Where(c => c.IsChecked).Select(c => c.Device).ToList();
        if (targets is [])
        {
            throw new InvalidOperationException(Loc.Get("LinkSelectRequired"));
        }

        await MusicCastLink.LinkAsync(Device, targets.Select(t => t.Device), SelectedZoneId,
            groupName: string.IsNullOrWhiteSpace(GroupName) ? $"{Name} +{targets.Count}" : GroupName);
        foreach (var candidate in LinkCandidates)
        {
            candidate.IsChecked = false;
        }

        await Task.Delay(800);
        await RefreshLinkAsync();
        foreach (var target in targets)
        {
            await target.RefreshZoneAsync();
            await target.RefreshLinkAsync();
        }
    }

    private async Task UnlinkAsync()
    {
        var all = _allDevices().ToList();
        var devices = all.Select(a => a.Device);
        await (LinkRole == "server"
            ? MusicCastLink.DissolveAsync(Device, devices)
            : MusicCastLink.LeaveAsync(Device, devices, SelectedZoneId));

        await Task.Delay(800);
        foreach (var d in all)
        {
            await d.RefreshLinkAsync();
        }
    }
}
