using System.Collections.Frozen;
using System.Globalization;
using System.Windows.Media.Imaging;
using Mcctl.App.Mvvm;

namespace Mcctl.App.ViewModels;

public sealed record OptionItem(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record SleepOption(int Minutes, string Name)
{
    public override string ToString() => Name;
}

/// <param name="IsContainer">Folder: double click enters it.</param>
/// <param name="IsPlayable">Playable item: shows the play button.</param>
/// <param name="IsSearchable">Search entry (e.g. Net Radio \u2192 Radio \u2192 \u691C\u7D22\u3059\u308B): asks for a search string.</param>
public sealed record BrowseItem(int Index, string Text, Uri? Thumbnail, bool IsContainer, bool IsPlayable, bool NowPlaying, bool IsSearchable)
{
    /// <summary>Segoe Fluent Icons: now playing / search / folder / music note.</summary>
    public string Glyph => NowPlaying ? "\uE768" : IsSearchable ? "\uE721" : IsContainer ? "\uE8B7" : "\uE8D6";

    /// <summary>Plain folder (shows the "open" arrow); search entries show the search button instead.</summary>
    public bool IsFolder => IsContainer && !IsSearchable;
}

public enum PresetKind
{
    Net,
    Tuner,
}

public sealed record PresetItem(PresetKind Kind, int Number, string Title, string Subtitle);

public sealed record RecentItemVm(int Number, string Title, string Input, Uri? Art);

public sealed class LinkCandidate(DeviceViewModel device) : ObservableObject
{
    public DeviceViewModel Device { get; } = device;

    public bool IsChecked { get; set => Set(ref field, value); }
}

public static class DisplayNames
{
    private static readonly FrozenDictionary<string, string> Known = new Dictionary<string, string>
    {
        ["net_radio"] = "Net Radio",
        ["server"] = "Server (DLNA)",
        ["usb"] = "USB",
        ["bluetooth"] = "Bluetooth",
        ["airplay"] = "AirPlay",
        ["spotify"] = "Spotify",
        ["tidal"] = "TIDAL",
        ["deezer"] = "Deezer",
        ["qobuz"] = "Qobuz",
        ["amazon_music"] = "Amazon Music",
        ["napster"] = "Napster",
        ["pandora"] = "Pandora",
        ["siriusxm"] = "SiriusXM",
        ["radiko"] = "radiko",
        ["alexa"] = "Alexa",
        ["chromecast"] = "Chromecast",
        ["mc_link"] = "MusicCast Link",
        ["tuner"] = "Tuner",
        ["cd"] = "CD",
        ["tv"] = "TV",
        ["optical"] = "Optical",
        ["optical1"] = "Optical 1",
        ["optical2"] = "Optical 2",
        ["coaxial"] = "Coaxial",
        ["coaxial1"] = "Coaxial 1",
        ["coaxial2"] = "Coaxial 2",
        ["aux"] = "AUX",
        ["aux1"] = "AUX 1",
        ["aux2"] = "AUX 2",
        ["line1"] = "Line 1",
        ["line2"] = "Line 2",
        ["line3"] = "Line 3",
        ["phono"] = "Phono",
        ["av1"] = "AV 1",
        ["av2"] = "AV 2",
        ["av3"] = "AV 3",
        ["hdmi"] = "HDMI",
        ["main_sync"] = "Main Zone Sync",
        ["2ch_stereo"] = "2ch Stereo",
        ["7ch_stereo"] = "7ch Stereo",
        ["9ch_stereo"] = "9ch Stereo",
        ["11ch_stereo"] = "11ch Stereo",
        ["all_ch_stereo"] = "All-Ch Stereo",
        ["straight"] = "Straight",
        ["surr_decoder"] = "Surround Decoder",
        ["3d_movie"] = "3D Movie",
        ["tv_program"] = "TV Program",
        ["mono_movie"] = "Mono Movie",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ABC order, ignoring case, with numbers compared by value ("HDMI 2" before "HDMI 10").
    /// </summary>
    public static StringComparer NaturalOrder { get; } =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

    public static string Get(string id, IReadOnlyDictionary<string, string>? deviceNames = null)
        => deviceNames?.GetValueOrDefault(id) is { Length: > 0 } fromDevice ? fromDevice
            : Known.TryGetValue(id, out var known) ? known
            : id.StartsWith("hdmi", StringComparison.OrdinalIgnoreCase) ? $"HDMI {id[4..]}"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' '));

    /// <summary>m:ss, or h:mm:ss for an hour or more; negative values (unknown) show as --:--.</summary>
    public static string FormatTime(int seconds)
        => seconds < 0 ? "--:--" : TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);

    public static string FormatFrequency(string band, int kHz) => band switch
    {
        "fm" => $"FM {kHz / 1000.0:0.00} MHz",
        "am" => $"AM {kHz} kHz",
        _ => $"{band.ToUpperInvariant()} {kHz}",
    };
}

public static class ImageLoader
{
    public static BitmapImage? FromBytes(byte[] data)
    {
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(data);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record LanguageOption(Localization.AppLanguage Language, string Name)
{
    public override string ToString() => Name;
}
