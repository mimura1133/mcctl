using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>tuner/*</c> endpoints (AM / FM / DAB).</summary>
public sealed class TunerApi(YxcClient client)
{
    public Task<TunerPlayInfo> GetPlayInfoAsync(CancellationToken ct = default)
        => client.GetAsync<TunerPlayInfo>("tuner/getPlayInfo", null, ct);

    /// <param name="band">"common" (shared preset list), "am", "fm" or "dab".</param>
    public Task<TunerPresetInfo> GetPresetInfoAsync(string band = "common", CancellationToken ct = default)
        => client.GetAsync<TunerPresetInfo>("tuner/getPresetInfo", Query.Of("band", band), ct);

    /// <param name="band">"am", "fm", "dab".</param>
    public Task<YxcResponse> SetBandAsync(string band, CancellationToken ct = default)
        => client.GetAsync("tuner/setBand", Query.Of("band", band), ct);

    /// <summary>Tunes directly to a frequency in kHz (e.g. 80000 for FM 80.0 MHz).</summary>
    public Task<YxcResponse> SetFrequencyAsync(string band, int kHz, CancellationToken ct = default)
        => client.GetAsync("tuner/setFreq", new Query().Add("band", band).Add("tuning", "direct").Add("num", kHz), ct);

    /// <param name="tuning">"up", "down", "cancel", "auto_up", "auto_down", "tp_up", "tp_down".</param>
    public Task<YxcResponse> TuneAsync(string band, string tuning, CancellationToken ct = default)
        => client.GetAsync("tuner/setFreq", new Query().Add("band", band).Add("tuning", tuning), ct);

    /// <param name="band">"common", "am", "fm" or "dab" – must match the preset type from getFeatures.</param>
    public Task<YxcResponse> RecallPresetAsync(int num, string band = "common", string zone = ZoneIds.Main, CancellationToken ct = default)
        => client.GetAsync("tuner/recallPreset", new Query().Add("zone", zone).Add("band", band).Add("num", num), ct);

    /// <param name="dir">"next" or "previous".</param>
    public Task<YxcResponse> SwitchPresetAsync(string dir, CancellationToken ct = default)
        => client.GetAsync("tuner/switchPreset", Query.Of("dir", dir), ct);

    public Task<YxcResponse> StorePresetAsync(int num, CancellationToken ct = default)
        => client.GetAsync("tuner/storePreset", Query.Of("num", num), ct);

    public Task<YxcResponse> ClearPresetAsync(int num, string band = "common", CancellationToken ct = default)
        => client.GetAsync("tuner/clearPreset", new Query().Add("band", band).Add("num", num), ct);

    /// <param name="dir">"next" / "previous" DAB service.</param>
    public Task<YxcResponse> SetDabServiceAsync(string dir, CancellationToken ct = default)
        => client.GetAsync("tuner/setDabService", Query.Of("dir", dir), ct);

    public Task<YxcResponse> StartAutoPresetAsync(string band = "fm", CancellationToken ct = default)
        => client.GetAsync("tuner/startAutoPreset", Query.Of("band", band), ct);

    public Task<YxcResponse> CancelAutoPresetAsync(string band = "fm", CancellationToken ct = default)
        => client.GetAsync("tuner/cancelAutoPreset", Query.Of("band", band), ct);
}
