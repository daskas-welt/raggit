using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// <see cref="ILibraryPreferences"/> backed by LocalSettings when packaged
/// (MSIX) and a JSON file under LocalAppData when unpackaged (Debug F5),
/// because <c>ApplicationData.Current</c> throws without package identity.
/// </summary>
public sealed class WinUILibraryPreferences : ILibraryPreferences
{
    private const string FileName = "prefs.json";
    private const string Key = "RAGGit.LibraryPageSize";

    public int GetPageSize()
    {
        var raw =
            WinUIPackaging.IsPackaged
                ? Windows.Storage.ApplicationData.Current.LocalSettings.Values[Key] switch
                {
                    int i => i,
                    long l => (int)l,
                    _ => LibraryPageSizes.Default,
                }
            : int.TryParse(
                WinUIPackaging.ReadJsonSetting(FileName, Key, string.Empty),
                out var parsed
            )
                ? parsed
            : LibraryPageSizes.Default;

        return LibraryPageSizes.IsValid(raw) ? raw : LibraryPageSizes.Default;
    }

    public void SetPageSize(int size)
    {
        if (WinUIPackaging.IsPackaged)
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[Key] = size;
        }
        else
        {
            WinUIPackaging.WriteJsonSetting(FileName, Key, size.ToString());
        }
    }
}
