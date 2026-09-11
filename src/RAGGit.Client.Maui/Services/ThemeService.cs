#if MAUI
using Syncfusion.Maui.Themes;
#endif

namespace RAGGit.Client.Maui.Services;

/// <summary>Runtime MaterialLight/Dark switch for Syncfusion theming (Toolkit + DataGrid + AIAssistView).</summary>
public static class ThemeService
{
#if MAUI
    public static void SetTheme(string visualTheme)
    {
        var app = Application.Current;
        if (app is null)
            return;
        var merged = app.Resources.MergedDictionaries;
        var existing = merged.OfType<SyncfusionThemeResourceDictionary>().FirstOrDefault();
        if (existing is not null)
            merged.Remove(existing);
        merged.Insert(0, new SyncfusionThemeResourceDictionary { VisualTheme = visualTheme });
    }

    public static void Toggle()
    {
        var app = Application.Current;
        var current = app
            ?.Resources.MergedDictionaries.OfType<SyncfusionThemeResourceDictionary>()
            .FirstOrDefault()
            ?.VisualTheme;
        SetTheme(current == "MaterialLight" ? "MaterialDark" : "MaterialLight");
    }
#else
    public static void SetTheme(string _) { }

    public static void Toggle() { }
#endif
}
