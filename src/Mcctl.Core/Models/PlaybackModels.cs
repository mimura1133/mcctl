namespace Mcctl.Models;

public sealed class NetUsbPlayInfo : YxcResponse
{
    public string Input { get; set; } = "";
    public string? PlayQueueType { get; set; }
    /// <summary>"play", "stop", "pause", "fast_reverse", "fast_forward".</summary>
    public string Playback { get; set; } = "stop";
    /// <summary>"off", "one", "all".</summary>
    public string Repeat { get; set; } = "off";
    /// <summary>"off", "on", "songs", "albums".</summary>
    public string Shuffle { get; set; } = "off";
    public List<string>? RepeatAvailable { get; set; }
    public List<string>? ShuffleAvailable { get; set; }
    public int PlayTime { get; set; }
    public int TotalTime { get; set; }
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string Track { get; set; } = "";
    public string AlbumartUrl { get; set; } = "";
    public int AlbumartId { get; set; }
    public string? UsbDevicetype { get; set; }
    public bool AutoStopped { get; set; }
    public long Attribute { get; set; }
}

public sealed class ListInfo : YxcResponse
{
    public string Input { get; set; } = "";
    public int MenuLayer { get; set; }
    public int MaxLine { get; set; }
    public int Index { get; set; }
    public int PlayingIndex { get; set; } = -1;
    public string MenuName { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("list_info")]
    public List<ListItem> ListInfoItems { get; set; } = [];
}

public sealed class ListItem
{
    public string Text { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public long Attribute { get; set; }

    public ListItemAttribute Flags => (ListItemAttribute)Attribute;

    /// <summary>A folder that is entered with <c>select</c>.</summary>
    public bool IsContainer => Flags.HasFlag(ListItemAttribute.Container);

    /// <summary>An item that can be played directly.</summary>
    public bool IsPlayable => Flags.HasFlag(ListItemAttribute.Playable);

    /// <summary>Search entry: needs a search string (<c>netusb/setSearchString</c>) instead of a plain select.</summary>
    public bool IsSearchable => Flags.HasFlag(ListItemAttribute.CapableOfSearch);
}

public sealed class NetUsbPresetInfo : YxcResponse
{
    public List<NetUsbPreset> PresetInfo { get; set; } = [];
    public List<string>? FuncList { get; set; }
}

public sealed class NetUsbPreset
{
    public string Input { get; set; } = "";
    public string Text { get; set; } = "";
    public long Attribute { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(Input) || Input == "unknown";
}

public sealed class RecentInfo : YxcResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("recent_info")]
    public List<RecentItem> RecentInfoItems { get; set; } = [];
}

public sealed class RecentItem
{
    public string Input { get; set; } = "";
    public string Text { get; set; } = "";
    public string AlbumartUrl { get; set; } = "";
    public int PlayCount { get; set; }
    public long Attribute { get; set; }
}

public sealed class TunerPlayInfo : YxcResponse
{
    /// <summary>"am", "fm", "dab".</summary>
    public string Band { get; set; } = "fm";
    public bool AutoScan { get; set; }
    public bool AutoPreset { get; set; }
    public TunerBandInfo? Am { get; set; }
    public TunerBandInfo? Fm { get; set; }
    public RdsInfo? Rds { get; set; }
    public DabInfo? Dab { get; set; }
}

public sealed class TunerBandInfo
{
    public int Preset { get; set; }
    /// <summary>kHz.</summary>
    public int Freq { get; set; }
    public bool Tuned { get; set; }
    public string? AudioMode { get; set; }
}

public sealed class RdsInfo
{
    public string ProgramType { get; set; } = "";
    public string ProgramService { get; set; } = "";
    public string RadioTextA { get; set; } = "";
    public string RadioTextB { get; set; } = "";
}

public sealed class DabInfo
{
    public int Preset { get; set; }
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public int Freq { get; set; }
    public string Category { get; set; } = "";
    public string AudioMode { get; set; } = "";
    public int BitRate { get; set; }
    public int Quality { get; set; }
    public string ChLabel { get; set; } = "";
    public string ServiceLabel { get; set; } = "";
    public string Dls { get; set; } = "";
    public string EnsembleLabel { get; set; } = "";
}

public sealed class TunerPresetInfo : YxcResponse
{
    public List<TunerPreset> PresetInfo { get; set; } = [];
    public List<string>? FuncList { get; set; }
}

public sealed class TunerPreset
{
    public string Band { get; set; } = "";
    /// <summary>Frequency in kHz (AM/FM) or DAB station id.</summary>
    public int Number { get; set; }
    public int? HdProgram { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(Band) || Band == "unknown" || Number == 0;
}

public sealed class CdPlayInfo : YxcResponse
{
    /// <summary>"open", "close", "ready", "not_ready".</summary>
    public string DeviceStatus { get; set; } = "";
    public string Playback { get; set; } = "stop";
    public string Repeat { get; set; } = "off";
    public string Shuffle { get; set; } = "off";
    public int PlayTime { get; set; }
    public int TotalTime { get; set; }
    public int DiscTime { get; set; }
    public int TrackNumber { get; set; }
    public int TotalTracks { get; set; }
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string Track { get; set; } = "";
}

public sealed class DistributionInfo : YxcResponse
{
    public string GroupId { get; set; } = "";
    public string GroupName { get; set; } = "";
    /// <summary>"server", "client" or "none".</summary>
    public string Role { get; set; } = "none";
    public string? ServerZone { get; set; }
    public List<DistributionClient> ClientList { get; set; } = [];
    /// <summary>Boolean on some firmware, an array of reasons on others (e.g. R-N1000A).</summary>
    public System.Text.Json.JsonElement? BuildDisable { get; set; }
    public bool? AudioDropout { get; set; }

    public bool HasGroup => !string.IsNullOrEmpty(GroupId) && GroupId.Trim('0').Length > 0;
}

public sealed class DistributionClient
{
    public string IpAddress { get; set; } = "";
    public string DataType { get; set; } = "";
}
