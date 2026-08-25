using HDecompressHelper.Models;

namespace HDecompressHelper.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    void Apply(AppTheme theme);
}

