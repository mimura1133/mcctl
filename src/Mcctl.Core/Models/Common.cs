using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcctl.Models;

/// <summary>Base of every YXC response. <c>response_code</c> 0 means success.</summary>
public class YxcResponse
{
    public int ResponseCode { get; set; }

    /// <summary>Fields not mapped to a typed property (newer firmware, model specific).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class RangeStep
{
    public string Id { get; set; } = "";
    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; } = 1;
}

/// <summary>
/// Attribute flags of a list / preset / recent item.
/// Verified on R-N1000A (API 2.17): folders (servers, NAS folders, radiko regions) report 0x2,
/// playable entries (radiko stations) report 0x4. Higher bits are service specific and may be set too.
/// </summary>
[Flags]
public enum ListItemAttribute : long
{
    None = 0,
    /// <summary>Folder / container: <c>setListControl type=select</c> enters it.</summary>
    Container = 1 << 1,
    /// <summary>Playable item: <c>setListControl type=play</c> (or select) starts it.</summary>
    Playable = 1 << 2,
    /// <summary>Search entry (e.g. Net Radio "検索する" reports 0x2 | 0x8): use netusb/setSearchString.</summary>
    CapableOfSearch = 1 << 3,
    AlbumArtAvailable = 1 << 4,
    NowPlaying = 1 << 5,
}

/// <summary>Well known zone identifiers.</summary>
public static class ZoneIds
{
    public const string Main = "main";
    public const string Zone2 = "zone2";
    public const string Zone3 = "zone3";
    public const string Zone4 = "zone4";
}

/// <summary>Values of <c>play_info_type</c> in <c>system/getFeatures</c> input list.</summary>
public static class PlayInfoTypes
{
    public const string NetUsb = "netusb";
    public const string Tuner = "tuner";
    public const string Cd = "cd";
    public const string None = "none";
}
