using System.Windows;
using System.Windows.Media;
using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["CanvasBrush"] = "#0A0D12",
        ["Surface0Brush"] = "#0E131A",
        ["Surface1Brush"] = "#121923",
        ["Surface2Brush"] = "#18212D",
        ["BorderBrush"] = "#253142",
        ["TextPrimaryBrush"] = "#F2F6FA",
        ["TextSecondaryBrush"] = "#98A6B8",
        ["TextMutedBrush"] = "#647286",
        ["AccentBrush"] = "#35C8F5",
        ["AccentHoverBrush"] = "#66D8FA",
        ["SuccessBrush"] = "#42D69B",
        ["WarningBrush"] = "#F5BE50",
        ["DangerBrush"] = "#FF647C",
        ["AccentTintBrush"] = "#1935C8F5",
        ["SuccessTintBrush"] = "#1942D69B",
        ["WarningTintBrush"] = "#19F5BE50",
        ["DangerTintBrush"] = "#19FF647C",
        ["PrimaryButtonTextBrush"] = "#061018",
        ["LogTextBrush"] = "#C4D4E8"
    };

    private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
    {
        ["CanvasBrush"] = "#F3F6FA",
        ["Surface0Brush"] = "#FFFFFF",
        ["Surface1Brush"] = "#FFFFFF",
        ["Surface2Brush"] = "#EAF0F6",
        ["BorderBrush"] = "#CDD8E6",
        ["TextPrimaryBrush"] = "#152033",
        ["TextSecondaryBrush"] = "#52647B",
        ["TextMutedBrush"] = "#7A899D",
        ["AccentBrush"] = "#078FC9",
        ["AccentHoverBrush"] = "#0576A8",
        ["SuccessBrush"] = "#07865E",
        ["WarningBrush"] = "#A96500",
        ["DangerBrush"] = "#D13D57",
        ["AccentTintBrush"] = "#17078FC9",
        ["SuccessTintBrush"] = "#1707865E",
        ["WarningTintBrush"] = "#17A96500",
        ["DangerTintBrush"] = "#17D13D57",
        ["PrimaryButtonTextBrush"] = "#FFFFFF",
        ["LogTextBrush"] = "#29415F"
    };

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        var palette = theme == AppTheme.Light ? LightPalette : DarkPalette;
        foreach (var (key, value) in palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(value);
            if (Application.Current.TryFindResource(key) is SolidColorBrush { IsFrozen: false } brush)
            {
                brush.Color = color;
            }
            else
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
            }
        }

        CurrentTheme = theme;
    }
}
