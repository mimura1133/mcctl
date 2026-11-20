namespace Mcctl.Models;

public sealed class DeviceInfo : YxcResponse
{
    public string ModelName { get; set; } = "";
    public string Destination { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string SystemId { get; set; } = "";
    public double SystemVersion { get; set; }
    public double ApiVersion { get; set; }
    public string NetmoduleVersion { get; set; } = "";
    public string NetmoduleChecksum { get; set; } = "";
    public string OperationMode { get; set; } = "";
    public int UpdateErrorCode { get; set; }
}

public sealed class Features : YxcResponse
{
    public SystemFeatures System { get; set; } = new();
    public List<ZoneFeatures> Zone { get; set; } = [];
    public TunerFeatures? Tuner { get; set; }
    public NetUsbFeatures? Netusb { get; set; }
    public DistributionFeatures? Distribution { get; set; }

    public ZoneFeatures? FindZone(string id) => Zone.FirstOrDefault(z => z.Id == id);

    public InputFeature? FindInput(string id) => System.InputList.FirstOrDefault(i => i.Id == id);
}

public sealed class SystemFeatures
{
    public List<string> FuncList { get; set; } = [];
    public int ZoneNum { get; set; } = 1;
    public List<InputFeature> InputList { get; set; } = [];
    public List<RangeStep> RangeStep { get; set; } = [];
}

public sealed class InputFeature
{
    public string Id { get; set; } = "";
    public bool DistributionEnable { get; set; }
    public bool RenameEnable { get; set; }
    public bool AccountEnable { get; set; }
    public string PlayInfoType { get; set; } = PlayInfoTypes.None;
}

public sealed class ZoneFeatures
{
    public string Id { get; set; } = ZoneIds.Main;
    public int ZoneB { get; set; }
    public List<string> FuncList { get; set; } = [];
    public List<string> InputList { get; set; } = [];
    public List<string> SoundProgramList { get; set; } = [];
    public List<string> SurrDecoderTypeList { get; set; } = [];
    public List<string> ToneControlModeList { get; set; } = [];
    public List<string> EqualizerModeList { get; set; } = [];
    public List<string> LinkControlList { get; set; } = [];
    public List<string> LinkAudioDelayList { get; set; } = [];
    public List<RangeStep> RangeStep { get; set; } = [];

    public bool Supports(string func) => FuncList.Contains(func);

    public RangeStep? Range(string id) => RangeStep.FirstOrDefault(r => r.Id == id);
}

public sealed class TunerFeatures
{
    public List<string> FuncList { get; set; } = [];
    public List<RangeStep> RangeStep { get; set; } = [];
    public TunerPresetFeature? Preset { get; set; }
}

public sealed class TunerPresetFeature
{
    public string Type { get; set; } = "common";
    public int Num { get; set; }
}

public sealed class NetUsbFeatures
{
    public List<string> FuncList { get; set; } = [];
    public CountFeature? Preset { get; set; }
    public CountFeature? RecentInfo { get; set; }
    public string NetRadioType { get; set; } = "";
}

public sealed class CountFeature
{
    public int Num { get; set; }
}

public sealed class DistributionFeatures
{
    public double Version { get; set; }
    public List<int> CompatibleClient { get; set; } = [];
    public int ClientMax { get; set; }
    public List<string> ServerZoneList { get; set; } = [];
}

public sealed class NetworkStatus : YxcResponse
{
    public string NetworkName { get; set; } = "";
    public string Connection { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string SubnetMask { get; set; } = "";
    public string DefaultGateway { get; set; } = "";
    public bool DhcpEnable { get; set; }
    public Dictionary<string, string>? MacAddress { get; set; }
}

public sealed class LocationInfo : YxcResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, bool>? ZoneList { get; set; }
}

public sealed class NameText : YxcResponse
{
    public List<IdText>? ZoneList { get; set; }
    public List<IdText>? InputList { get; set; }
    public List<IdText>? SoundProgramList { get; set; }

    /// <summary>Returned when a single id is requested.</summary>
    public string? Id { get; set; }
    public string? Text { get; set; }
}

public sealed class IdText
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class FuncStatus : YxcResponse
{
    public bool? AutoPowerStandby { get; set; }
    public bool? Ir { get; set; }
    public bool? SpeakerA { get; set; }
    public bool? SpeakerB { get; set; }
    public string? HeadphoneTypes { get; set; }
    public string? DimmerLevel { get; set; }
}
