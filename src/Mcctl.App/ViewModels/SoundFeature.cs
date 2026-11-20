using Mcctl.Api;
using Mcctl.App.Mvvm;
using Mcctl.Models;

namespace Mcctl.App.ViewModels;

/// <summary>
/// An on/off sound feature of a zone. Reading, writing and labelling live in one table
/// instead of being spread over separate switch statements.
/// </summary>
public sealed record SoundFeature(string Func, string Label, Func<ZoneStatus, bool?> Read, Func<ZoneApi, bool, Task> Write)
{
    public static IReadOnlyList<SoundFeature> All { get; } =
    [
        new("pure_direct", "Pure Direct", s => s.PureDirect, (z, on) => z.SetPureDirectAsync(on)),
        new("direct", "Direct", s => s.Direct, (z, on) => z.SetDirectAsync(on)),
        new("enhancer", "Compressed Music Enhancer", s => s.Enhancer, (z, on) => z.SetEnhancerAsync(on)),
        new("clear_voice", "Clear Voice", s => s.ClearVoice, (z, on) => z.SetClearVoiceAsync(on)),
        new("bass_extension", "Bass Extension", s => s.BassExtension, (z, on) => z.SetBassExtensionAsync(on)),
        new("extra_bass", "Extra Bass", s => s.ExtraBass, (z, on) => z.SetExtraBassAsync(on)),
        new("adaptive_drc", "Adaptive DRC", s => s.AdaptiveDrc, (z, on) => z.SetAdaptiveDrcAsync(on)),
    ];
}

/// <summary>Check box state for one <see cref="SoundFeature"/>.</summary>
public sealed class FeatureToggle(SoundFeature feature) : ObservableObject
{
    public SoundFeature Feature { get; } = feature;

    public string Label => Feature.Label;

    public bool State { get; set => Set(ref field, value); }
}
