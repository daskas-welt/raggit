using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
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
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is RAGGit.Client.Maui.Services.UserAccountDto user
            ? $"Selected: {user.Username}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
