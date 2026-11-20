using System.Windows;
using System.Windows.Controls;

namespace Mcctl.App.Controls;

/// <summary>
/// Lays out exactly two children as one block: a square artwork (first child) above a caption
/// (second child). The artwork takes whatever height the caption leaves, limited by the width and
/// <see cref="MaxArtSize"/>, and the whole block is centred vertically – so when the window is resized
/// the image and the text grow, shrink and move together instead of drifting apart.
/// </summary>
public sealed class ArtworkStackPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(ArtworkStackPanel),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxArtSizeProperty = DependencyProperty.Register(
        nameof(MaxArtSize), typeof(double), typeof(ArtworkStackPanel),
        new FrameworkPropertyMetadata(320.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MinArtSizeProperty = DependencyProperty.Register(
        nameof(MinArtSize), typeof(double), typeof(ArtworkStackPanel),
        new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Gap between the artwork and the caption.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double MaxArtSize
    {
        get => (double)GetValue(MaxArtSizeProperty);
        set => SetValue(MaxArtSizeProperty, value);
    }

    /// <summary>Below this size the artwork is hidden so the caption keeps the space.</summary>
    public double MinArtSize
    {
        get => (double)GetValue(MinArtSizeProperty);
        set => SetValue(MinArtSizeProperty, value);
    }

    private UIElement? Artwork => InternalChildren.Count > 0 ? InternalChildren[0] : null;

    private UIElement? Caption => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    protected override Size MeasureOverride(Size available)
    {
        Caption?.Measure(new Size(available.Width, double.PositiveInfinity));
        var captionHeight = Caption?.DesiredSize.Height ?? 0;

        var art = ArtSize(available, captionHeight);
        Artwork?.Measure(new Size(art, art));

        var width = Math.Max(art, Caption?.DesiredSize.Width ?? 0);
        return new Size(double.IsInfinity(available.Width) ? width : Math.Min(width, available.Width), BlockHeight(art, captionHeight));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var captionHeight = Caption?.DesiredSize.Height ?? 0;
        var art = ArtSize(final, captionHeight);
        var top = Math.Max(0, (final.Height - BlockHeight(art, captionHeight)) / 2);

        Artwork?.Arrange(new Rect((final.Width - art) / 2, top, art, art));
        Caption?.Arrange(new Rect(0, top + (art > 0 ? art + Spacing : 0), final.Width, captionHeight));
        return final;
    }

    private double ArtSize(Size space, double captionHeight)
    {
        var byHeight = double.IsInfinity(space.Height) ? double.PositiveInfinity : space.Height - captionHeight - Spacing;
        var size = Math.Min(Math.Min(space.Width, byHeight), MaxArtSize);
        return size >= MinArtSize ? Math.Floor(size) : 0;
    }

    private double BlockHeight(double art, double captionHeight) => (art > 0 ? art + Spacing : 0) + captionHeight;
}
