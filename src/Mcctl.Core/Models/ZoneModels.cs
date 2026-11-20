namespace Mcctl.Models;

public sealed class ZoneStatus : YxcResponse
{
    /// <summary>"on" or "standby".</summary>
    public string Power { get; set; } = "standby";
    public int Sleep { get; set; }
    public int Volume { get; set; }
    public bool Mute { get; set; }
    public int MaxVolume { get; set; }
    public ActualVolume? ActualVolume { get; set; }
    public string Input { get; set; } = "";
    public string? InputText { get; set; }
    public bool DistributionEnable { get; set; }
    public string? SoundProgram { get; set; }
    public string? SurrDecoderType { get; set; }
    public bool? Direct { get; set; }
    public bool? PureDirect { get; set; }
    public bool? Enhancer { get; set; }
    public ToneControl? ToneControl { get; set; }
    public Equalizer? Equalizer { get; set; }
    public int? Balance { get; set; }
    public int? DialogueLevel { get; set; }
    public int? DialogueLift { get; set; }
    public bool? ClearVoice { get; set; }
    public int? SubwooferVolume { get; set; }
    public bool? BassExtension { get; set; }
    public bool? ExtraBass { get; set; }
    public bool? AdaptiveDrc { get; set; }
    public string? LinkControl { get; set; }
    public string? LinkAudioDelay { get; set; }
    public string? LinkAudioQuality { get; set; }
    public long DisableFlags { get; set; }
    public bool? PartyEnable { get; set; }

    public bool IsOn => Power == "on";
}

public sealed class ActualVolume
{
    public string Mode { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}

public sealed class ToneControl
{
    public string Mode { get; set; } = "manual";
    public int Bass { get; set; }
    public int Treble { get; set; }
}

public sealed class Equalizer
{
    public string Mode { get; set; } = "manual";
    public int Low { get; set; }
    public int Mid { get; set; }
    public int High { get; set; }
}

public sealed class SoundProgramList : YxcResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("sound_program_list")]
    public List<string> SoundProgramListItems { get; set; } = [];
}

public sealed class SignalInfo : YxcResponse
{
    public AudioSignal? Audio { get; set; }
}

/// <summary>
/// Audio signal currently decoded by the zone, e.g. R-N1000A playing an MP3 from a server:
/// <c>{"error":0,"format":"MP3","fs":"44.1 kHz","bit":"","bitrate":0}</c>.
/// </summary>
public sealed class AudioSignal
{
    /// <summary>0 = no error.</summary>
    public int Error { get; set; }

    /// <summary>Codec / signal format ("MP3", "FLAC", "PCM", "Dolby Digital" …).</summary>
    public string Format { get; set; } = "";

    /// <summary>Sampling rate as text ("44.1 kHz").</summary>
    public string Fs { get; set; } = "";

    /// <summary>Bit depth as text ("24 bit"); empty for lossy formats.</summary>
    public string Bit { get; set; } = "";

    /// <summary>Bit rate in kbps; 0 when not reported.</summary>
    public int Bitrate { get; set; }

    /// <summary>Human readable summary, e.g. "FLAC · 96 kHz · 24 bit" or "MP3 · 44.1 kHz · 320 kbps".</summary>
    public string Describe()
        => string.Join(" · ", new[]
        {
            Format,
            Fs,
            Bit.Length > 0 && Bit.All(char.IsAsciiDigit) ? $"{Bit} bit" : Bit,
            Bitrate > 0 ? $"{Bitrate} kbps" : "",
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
