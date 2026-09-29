using System;
using System.Globalization;
using System.Windows.Data;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Converters;

/// <summary>
/// Thin WPF converters delegating to <see cref="DocumentDisplay"/>.
/// All formatting logic lives in Client.Core and is unit-tested there; these
/// types only adapt bound rows for XAML (mirrors the WinUI converters).
/// </summary>
public sealed class DocumentMimeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Document document ? DocumentDisplay.MimeLabel(document) : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class SizeMbConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Document document ? DocumentDisplay.FormatSizeMb(document) : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class CreatorLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Document document ? DocumentDisplay.CreatorLabel(document) : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Short date/time label for the Created column (WPF Binding has no StringFormat-free option here;
/// kept as a converter for parity with the WinUI version).
/// </summary>
public sealed class ShortDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTime dt ? dt.ToLocalTime().ToString("g") : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Replaces the MAUI MultiBinding citation subtitle:
/// "Document: {id} | Chunk: {id}".
/// </summary>
public sealed class CitationSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Citation citation
            ? $"Chunk {ShortId(citation.ChunkId)}  ·  Document {ShortId(citation.DocumentId)}"
            : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();

    private static string ShortId(Guid id) => id.ToString("N")[..8];
}

public sealed class CitationOrdinalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int ordinal ? $"[{ordinal + 1}]" : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

public sealed class CitationDocumentNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Citation citation
            ? !string.IsNullOrWhiteSpace(citation.DocumentName)
                ? citation.DocumentName
                : $"Document {citation.DocumentId.ToString("N")[..8]}"
            : string.Empty;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}

/// <summary>
/// Maps the UploadViewModel StatusSeverity string (Informational, Success,
/// Warning, Error — Core stays UI-framework free) to WPF-UI InfoBarSeverity.
/// </summary>
public sealed class StatusSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string severity
            ? severity switch
            {
                "Success" => InfoBarSeverity.Success,
                "Warning" => InfoBarSeverity.Warning,
                "Error" => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational,
            }
            : InfoBarSeverity.Informational;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
