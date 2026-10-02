using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Converters;

/// <summary>
/// WPF formatting adapters for values composed by earlier XAML bindings.
/// Pure formatting; all data comes from shared Core models.
/// </summary>
public sealed class HistoryMetaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is HistoryItem item
            ? $"Citations: {item.CitationCount} | {item.LatencyMs}ms | {item.CreatedAt.ToLocalTime():g}"
            : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class HistoryCitationSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is HistoryCitation citation
            ? $"[{citation.Ordinal}] Doc: {citation.DocumentId} / Chunk: {citation.ChunkId}"
            : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class MineMetaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DocumentMineItem item
            ? $"{item.Size} bytes | {item.CreatedAt.ToLocalTime():g}"
            : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class SelectedUserLabelConverter : IValueConverter
{
    // UserAccountDto is part of the shared Client.Core service contracts.
    //
    // 016-admin-redesign: always returns a visible line — either the selected
    // user or a hint to pick one — so the reset form never leaves selection
    // state ambiguous.
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UserAccountDto user
            ? $"Selected: {user.Username}"
            : "No user selected. Select a user in the list above to reset their password.";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// 016-admin-redesign: per-row accessible labels so every row action
/// announces which user it affects (keyboard + screen-reader operability).
/// Pure formatting; all data comes from Core models.
/// </summary>
public sealed class RoleActionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UserAccountDto user
            ? $"Change role for {user.Username}, currently {user.Role}"
            : "Change user role";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Verb for the active-state button: an active user can be deactivated and
/// vice versa. Clearly labeled text replaces the old glyph-only toggle.
/// </summary>
public sealed class ActiveVerbConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Deactivate" : "Activate";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class ActiveStatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Active" : "Disabled";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class LastSignInLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTime dateTime
            ? $"Last sign-in: {dateTime.ToLocalTime():g}"
            : "Never signed in";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class ActiveToggleLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UserAccountDto user
            ? $"{(user.IsActive ? "Deactivate" : "Activate")} {user.Username}"
            : "Toggle user active state";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class UserScopedAutomationIdConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UserAccountDto user && parameter is string prefix
            ? $"{prefix}_{user.Username}"
            : parameter?.ToString() ?? string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Lock state stays display-only (no behavior change): a text label plus a
/// per-row status name for screen readers.
/// </summary>
public sealed class LockedLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "Locked" : "Not locked";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class LockStatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UserAccountDto user
            ? $"Lock status for {user.Username}: {(user.LockedOut ? "Locked" : "Not locked")}"
            : "Lock status";

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Visible when a bound list is empty (mirrors the WinUI version).
/// </summary>
public sealed class EmptyListToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value switch
        {
            ICollection col => col.Count,
            null => 0,
            _ => 1,
        };
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// 028-pages-ux-polish (US2): per-row download busy. values[0] is the row
/// document id, values[1] the ViewModel's in-flight id set. Returns whether
/// that row is downloading — as a <see cref="bool"/> or, when the target is
/// <see cref="Visibility"/>, as Visible/Collapsed. The "Invert" parameter
/// flips the result (idle icon, row-button enabled state).
/// </summary>
public sealed class DocumentDownloadingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var downloading = values is [Guid id, IEnumerable<Guid> busy] && busy.Contains(id);
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            downloading = !downloading;
        }

        if (targetType == typeof(Visibility))
        {
            return downloading ? Visibility.Visible : Visibility.Collapsed;
        }

        return downloading;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
