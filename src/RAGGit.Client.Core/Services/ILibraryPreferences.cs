using System.Linq;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Valid page sizes plus validation for the library footer preference.
/// </summary>
public static class LibraryPageSizes
{
    public const int Default = 10;

    public static readonly int[] Valid = { 10, 25, 50, 100 };

    /// <summary>
    /// Whether <paramref name="size"/> is a selectable page size.
    /// </summary>
    public static bool IsValid(int size) => Valid.Contains(size);
}

/// <summary>
/// Seam for the persisted library page-size preference. The WinUI shell stores it
/// in LocalSettings (packaged) or a LocalAppData JSON file (unpackaged); tests
/// use <see cref="InMemoryLibraryPreferences"/>.
/// Never secure storage — a page-size setting is not a secret.
/// </summary>
public interface ILibraryPreferences
{
    /// <summary>
    /// Stored size if valid, else the 10 default (missing or corrupt values
    /// fall back; see spec edge cases).
    /// </summary>
    int GetPageSize();

    /// <summary>
    /// Persists the size; callers store only valid options.
    /// </summary>
    void SetPageSize(int size);
}

/// <summary>
/// Non-persisted test double: each instance starts unset (reads default).
/// </summary>
public sealed class InMemoryLibraryPreferences : ILibraryPreferences
{
    private int? _stored;

    public int GetPageSize() =>
        _stored is int stored && LibraryPageSizes.IsValid(stored)
            ? stored
            : LibraryPageSizes.Default;

    public void SetPageSize(int size) => _stored = size;
}
