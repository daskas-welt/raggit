using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.WinUI.Converters;

/// <summary>
/// WinUI replacements for MAUI MultiBinding/StringFormat expressions.
/// Pure formatting; all data comes from Core models.
/// </summary>
public sealed class HistoryMetaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is HistoryItem item
            ? $"Citations: {item.CitationCount} | {item.LatencyMs}ms | {item.CreatedAt.ToLocalTime():g}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HistoryCitationSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is HistoryCitation citation
            ? $"[{citation.Ordinal}] Doc: {citation.DocumentId} / Chunk: {citation.ChunkId}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class MineMetaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DocumentMineItem item
            ? $"{item.Size} bytes | {item.CreatedAt.ToLocalTime():g}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ActiveGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "\uE73E" : "\uE711";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class LockedGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "\uE72E" : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class SelectedUserLabelConverter : IValueConverter
{
    // NOTE: UserAccountDto still lives in the legacy RAGGit.Client.Maui.Services
    // namespace (Client.Core project). Full rename to RAGGit.Client.Core.Services
    // is tracked separately; this using avoids hard-coding the Maui name in XAML code-behind.
    //
    // 016-admin-redesign: always returns a visible line — either the selected
    // user or a hint to pick one — so the reset form never leaves selection
    // state ambiguous.
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is UserAccountDto user
            ? $"Selected: {user.Username}"
            : "No user selected. Select a user in the list above to reset their password.";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// 016-admin-redesign: per-row accessible labels so every row action
/// announces which user it affects (keyboard + screen-reader operability).
/// Pure formatting; all data comes from Core models.
/// </summary>
public sealed class RoleActionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is UserAccountDto user
            ? $"Change role for {user.Username}, currently {user.Role}"
            : "Change user role";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Verb for the active-state button: an active user can be deactivated and
/// vice versa. Clearly labeled text replaces the old glyph-only toggle.
/// </summary>
public sealed class ActiveVerbConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "Deactivate" : "Activate";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ActiveToggleLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is UserAccountDto user
            ? $"{(user.IsActive ? "Deactivate" : "Activate")} {user.Username}"
            : "Toggle user active state";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Lock state stays display-only (no behavior change): a text label plus a
/// per-row status name for screen readers.
/// </summary>
public sealed class LockedLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "Locked" : "Not locked";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class LockStatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is UserAccountDto user
            ? $"Lock status for {user.Username}: {(user.LockedOut ? "Locked" : "Not locked")}"
            : "Lock status";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Disables submit/refresh controls while the ViewModel is busy so duplicate
/// submits are prevented (016-admin-redesign FR-008).
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
