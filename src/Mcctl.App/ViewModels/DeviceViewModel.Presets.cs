using System.Collections.ObjectModel;
using Mcctl.App.Mvvm;

namespace Mcctl.App.ViewModels;

/// <summary>Net / tuner presets and recently played items.</summary>
public sealed partial class DeviceViewModel
{
    public ObservableCollection<PresetItem> NetPresets { get; } = [];

    public ObservableCollection<PresetItem> TunerPresets { get; } = [];

    public ObservableCollection<RecentItemVm> RecentItems { get; } = [];

    public int StorePresetNumber { get; set => Set(ref field, value); } = 1;

    public AsyncCommand RecallPresetCommand => field ??= new AsyncCommand<PresetItem>(RecallPresetAsync);
    public AsyncCommand StorePresetCommand => field ??= new AsyncCommand(StorePresetAsync);
    public AsyncCommand RecallRecentCommand => field ??= new AsyncCommand<RecentItemVm>(r => SendAndRefreshAsync(() => Device.NetUsb.RecallRecentItemAsync(r.Number, SelectedZoneId)));
    public AsyncCommand RefreshPresetsCommand => field ??= new AsyncCommand(RefreshPresetsAsync);

    /// <summary>"common" shares one list across bands; "separate" keeps one list per band.</summary>
    private string TunerPresetBand => Device.Features?.Tuner?.Preset?.Type switch
    {
        null => "common",
        "separate" => TunerBand,
        var type => type,
    };

    public async Task RefreshPresetsAsync()
    {
        try
        {
            if (Device.Features?.Netusb is not null)
            {
                var presets = await Device.NetUsb.GetPresetInfoAsync();
                ReplaceAll(NetPresets, presets.PresetInfo
                    .Select((p, i) => (Preset: p, Number: i + 1))
                    .Where(x => !x.Preset.IsEmpty)
                    .Select(x => new PresetItem(PresetKind.Net, x.Number, x.Preset.Text, DisplayNames.Get(x.Preset.Input, _names))));

                var recent = await Device.NetUsb.GetRecentInfoAsync();
                ReplaceAll(RecentItems, recent.RecentInfoItems
                    .Select((r, i) => (Item: r, Number: i + 1))
                    .Where(x => x.Item.Input is not ("" or "unknown"))
                    .Select(x => new RecentItemVm(x.Number, x.Item.Text, DisplayNames.Get(x.Item.Input, _names), Device.ResolveUrl(x.Item.AlbumartUrl))));
            }

            if (Device.Features?.Tuner is not null)
            {
                var presets = await Device.Tuner.GetPresetInfoAsync(TunerPresetBand);
                ReplaceAll(TunerPresets, presets.PresetInfo
                    .Select((p, i) => (Preset: p, Number: i + 1))
                    .Where(x => !x.Preset.IsEmpty)
                    .Select(x => new PresetItem(PresetKind.Tuner, x.Number,
                        DisplayNames.FormatFrequency(x.Preset.Band, x.Preset.Number), x.Preset.Band.ToUpperInvariant())));
            }
        }
        catch (Exception ex) when (ex.IsDeviceError)
        {
            // Presets are optional; keep what is shown.
        }
    }

    private async Task RecallPresetAsync(PresetItem item)
    {
        await (item.Kind == PresetKind.Tuner
            ? Device.Tuner.RecallPresetAsync(item.Number, TunerPresetBand, SelectedZoneId)
            : Device.NetUsb.RecallPresetAsync(item.Number, SelectedZoneId));
        await Task.Delay(500);
        await RefreshZoneAsync();
        await RefreshPlayInfoAsync();
    }

    private async Task StorePresetAsync()
    {
        await (IsTuner ? Device.Tuner.StorePresetAsync(StorePresetNumber) : Device.NetUsb.StorePresetAsync(StorePresetNumber));
        await RefreshPresetsAsync();
    }
}
