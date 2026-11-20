using Mcctl.Models;

namespace Mcctl.Api;

/// <summary><c>{zone}/*</c> endpoints (main, zone2, zone3, zone4).</summary>
public sealed class ZoneApi(YxcClient client, string zone)
{
    public string Zone { get; } = zone;

    private string P(string method) => $"{Zone}/{method}";

    public Task<ZoneStatus> GetStatusAsync(CancellationToken ct = default)
        => client.GetAsync<ZoneStatus>(P("getStatus"), null, ct);

    public Task<SoundProgramList> GetSoundProgramListAsync(CancellationToken ct = default)
        => client.GetAsync<SoundProgramList>(P("getSoundProgramList"), null, ct);

    public Task<SignalInfo> GetSignalInfoAsync(CancellationToken ct = default)
        => client.GetAsync<SignalInfo>(P("getSignalInfo"), null, ct);

    public Task<YxcResponse> SetPowerAsync(PowerState power, CancellationToken ct = default)
        => client.GetAsync(P("setPower"), Query.Of("power", power), ct);

    /// <param name="minutes">0, 30, 60, 90 or 120.</param>
    public Task<YxcResponse> SetSleepAsync(int minutes, CancellationToken ct = default)
        => client.GetAsync(P("setSleep"), Query.Of("sleep", minutes), ct);

    public Task<YxcResponse> SetVolumeAsync(int volume, CancellationToken ct = default)
        => client.GetAsync(P("setVolume"), Query.Of("volume", volume), ct);

    public Task<YxcResponse> VolumeUpAsync(int step = 1, CancellationToken ct = default)
        => client.GetAsync(P("setVolume"), new Query().Add("volume", "up").Add("step", step), ct);

    public Task<YxcResponse> VolumeDownAsync(int step = 1, CancellationToken ct = default)
        => client.GetAsync(P("setVolume"), new Query().Add("volume", "down").Add("step", step), ct);

    /// <summary>Volume in dB (API 2.x AV receivers).</summary>
    public Task<YxcResponse> SetActualVolumeAsync(double db, CancellationToken ct = default)
        => client.GetAsync(P("setActualVolume"), new Query().Add("mode", "db").Add("value", db), ct);

    public Task<YxcResponse> SetMuteAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setMute"), Query.Of("enable", enable), ct);

    /// <param name="input">Input id from <see cref="ZoneFeatures.InputList"/>.</param>
    /// <param name="mode">"autoplay_disabled" prevents auto playback of net inputs.</param>
    public Task<YxcResponse> SetInputAsync(string input, string? mode = null, CancellationToken ct = default)
        => client.GetAsync(P("setInput"), new Query().Add("input", input).Add("mode", mode), ct);

    public Task<YxcResponse> PrepareInputChangeAsync(string input, CancellationToken ct = default)
        => client.GetAsync(P("prepareInputChange"), Query.Of("input", input), ct);

    public Task<YxcResponse> SetSoundProgramAsync(string program, CancellationToken ct = default)
        => client.GetAsync(P("setSoundProgram"), Query.Of("program", program), ct);

    public Task<YxcResponse> SetSurroundDecoderAsync(string type, CancellationToken ct = default)
        => client.GetAsync(P("setSurroundDecoderType"), Query.Of("type", type), ct);

    public Task<YxcResponse> SetDirectAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setDirect"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetPureDirectAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setPureDirect"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetEnhancerAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setEnhancer"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetClearVoiceAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setClearVoice"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetBassExtensionAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setBassExtension"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetExtraBassAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setExtraBass"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetAdaptiveDrcAsync(bool enable, CancellationToken ct = default)
        => client.GetAsync(P("setAdaptiveDrc"), Query.Of("enable", enable), ct);

    public Task<YxcResponse> SetToneControlAsync(int? bass, int? treble, string mode = "manual", CancellationToken ct = default)
        => client.GetAsync(P("setToneControl"), new Query().Add("mode", mode).Add("bass", bass).Add("treble", treble), ct);

    public Task<YxcResponse> SetEqualizerAsync(int? low, int? mid, int? high, string mode = "manual", CancellationToken ct = default)
        => client.GetAsync(P("setEqualizer"), new Query().Add("mode", mode).Add("low", low).Add("mid", mid).Add("high", high), ct);

    public Task<YxcResponse> SetBalanceAsync(int value, CancellationToken ct = default)
        => client.GetAsync(P("setBalance"), Query.Of("value", value), ct);

    public Task<YxcResponse> SetDialogueLevelAsync(int value, CancellationToken ct = default)
        => client.GetAsync(P("setDialogueLevel"), Query.Of("value", value), ct);

    public Task<YxcResponse> SetDialogueLiftAsync(int value, CancellationToken ct = default)
        => client.GetAsync(P("setDialogueLift"), Query.Of("value", value), ct);

    public Task<YxcResponse> SetSubwooferVolumeAsync(int volume, CancellationToken ct = default)
        => client.GetAsync(P("setSubwooferVolume"), Query.Of("volume", volume), ct);

    /// <param name="control">"standard", "stability", "speed".</param>
    public Task<YxcResponse> SetLinkControlAsync(string control, CancellationToken ct = default)
        => client.GetAsync(P("setLinkControl"), Query.Of("control", control), ct);

    /// <param name="delay">"audio_sync", "lip_sync", "balanced" ...</param>
    public Task<YxcResponse> SetLinkAudioDelayAsync(string delay, CancellationToken ct = default)
        => client.GetAsync(P("setLinkAudioDelay"), Query.Of("delay", delay), ct);

    public Task<YxcResponse> SetLinkAudioQualityAsync(string mode, CancellationToken ct = default)
        => client.GetAsync(P("setLinkAudioQuality"), Query.Of("mode", mode), ct);

    /// <summary>Recall a scene (1-based) on AV receivers.</summary>
    public Task<YxcResponse> RecallSceneAsync(int num, CancellationToken ct = default)
        => client.GetAsync(P("recallScene"), Query.Of("num", num), ct);
}

public enum PowerState
{
    On,
    Standby,
    Toggle,
}
