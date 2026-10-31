namespace DOOM.Presentation.Themes;

using System.Windows.Media;

/// <summary>
/// Native Windows representation of the DOOM design tokens.
/// Exactly matches the macOS DOOM theme (DoomTheme.swift).
/// </summary>
public static class DoomColors
{
    // Raw Color Definitions
    public static readonly Color BgDeepColor = Color.FromRgb(0x0B, 0x0D, 0x10);
    public static readonly Color BgHighColor = Color.FromRgb(0x1B, 0x20, 0x26);
    public static readonly Color SurfaceColor = Color.FromRgb(0x14, 0x17, 0x1B);
    public static readonly Color SurfaceHighColor = Color.FromRgb(0x1C, 0x20, 0x26);
    public static readonly Color ControlColor = Color.FromRgb(0x2A, 0x30, 0x38);
    public static readonly Color MetalHighColor = Color.FromRgb(0xE6, 0xEA, 0xEE);
    public static readonly Color MetalLowColor = Color.FromRgb(0x8A, 0x94, 0x9E);
    public static readonly Color TextPrimaryColor = Color.FromRgb(0xE6, 0xEA, 0xEE);
    public static readonly Color TextSecondaryColor = Color.FromRgb(0x9A, 0xA4, 0xAE);
    public static readonly Color TextTertiaryColor = Color.FromRgb(0x8A, 0x94, 0x9E);
    public static readonly Color SteelColor = Color.FromRgb(0x5C, 0x66, 0x70);
    public static readonly Color AccentColor = Color.FromRgb(0x2F, 0xA3, 0x6B);
    public static readonly Color AccentTextColor = Color.FromRgb(0x4C, 0xC3, 0x8A);
    public static readonly Color WarningColor = Color.FromRgb(0xD9, 0xA4, 0x41);
    public static readonly Color ErrorColor = Color.FromRgb(0xE5, 0x67, 0x5F);
    public static readonly Color PrimaryButtonTextColor = Color.FromRgb(0x0B, 0x0D, 0x10);

    // Frozen SolidColorBrushes for performance and thread safety
    public static readonly SolidColorBrush BgDeepBrush = CreateFrozenBrush(BgDeepColor);
    public static readonly SolidColorBrush BgHighBrush = CreateFrozenBrush(BgHighColor);
    public static readonly SolidColorBrush SurfaceBrush = CreateFrozenBrush(SurfaceColor);
    public static readonly SolidColorBrush SurfaceHighBrush = CreateFrozenBrush(SurfaceHighColor);
    public static readonly SolidColorBrush ControlBrush = CreateFrozenBrush(ControlColor);
    public static readonly SolidColorBrush MetalHighBrush = CreateFrozenBrush(MetalHighColor);
    public static readonly SolidColorBrush MetalLowBrush = CreateFrozenBrush(MetalLowColor);
    public static readonly SolidColorBrush TextPrimaryBrush = CreateFrozenBrush(TextPrimaryColor);
    public static readonly SolidColorBrush TextSecondaryBrush = CreateFrozenBrush(TextSecondaryColor);
    public static readonly SolidColorBrush TextTertiaryBrush = CreateFrozenBrush(TextTertiaryColor);
    public static readonly SolidColorBrush SteelBrush = CreateFrozenBrush(SteelColor);
    public static readonly SolidColorBrush AccentBrush = CreateFrozenBrush(AccentColor);
    public static readonly SolidColorBrush AccentTextBrush = CreateFrozenBrush(AccentTextColor);
    public static readonly SolidColorBrush WarningBrush = CreateFrozenBrush(WarningColor);
    public static readonly SolidColorBrush ErrorBrush = CreateFrozenBrush(ErrorColor);
    public static readonly SolidColorBrush PrimaryButtonTextBrush = CreateFrozenBrush(PrimaryButtonTextColor);

    // Gradients
    public static readonly LinearGradientBrush WindowBackgroundBrush = CreateFrozenGradient(
        BgDeepColor, BgHighColor, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));

    public static readonly LinearGradientBrush PanelBackgroundBrush = CreateFrozenGradient(
        SurfaceColor, SurfaceHighColor, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));

    public static readonly LinearGradientBrush MetalBrush = CreateFrozenGradient(
        MetalHighColor, MetalLowColor, new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush CreateFrozenGradient(Color start, Color end, System.Windows.Point startPoint, System.Windows.Point endPoint)
    {
        var gradient = new LinearGradientBrush(start, end, startPoint, endPoint);
        gradient.Freeze();
        return gradient;
    }
}

