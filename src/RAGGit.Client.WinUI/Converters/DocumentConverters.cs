using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;

namespace RAGGit.Client.WinUI.Converters;

/// <summary>
/// Thin WinUI converters delegating to <see cref="DocumentDisplay"/>.
/// All formatting logic lives in Client.Core and is unit-tested there; these
/// types only adapt bound rows for XAML (mirrors the MAUI converters).
/// </summary>
public sealed class DocumentMimeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Document document ? DocumentDisplay.MimeLabel(document) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class SizeMbConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Document document ? DocumentDisplay.FormatSizeMb(document) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class CreatorLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Document document ? DocumentDisplay.CreatorLabel(document) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Short date/time label for the Created column (WinUI Binding has no StringFormat).
/// </summary>
public sealed class ShortDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DateTime dt ? dt.ToLocalTime().ToString("g") : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Replaces the MAUI MultiBinding citation subtitle:
/// "Document: {id} | Chunk: {id}".
/// </summary>
public sealed class CitationSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Citation citation
            ? $"Document: {citation.DocumentId} | Chunk: {citation.ChunkId}"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Visible when a bound list is empty (ListView has no EmptyView in WinUI).
/// </summary>
public sealed class EmptyListToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value switch
        {
            System.Collections.ICollection col => col.Count,
            null => 0,
            _ => 1,
        };
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
