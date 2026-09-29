using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Components;

/// <summary>
/// Reusable document-status indicator: themed status glyph + label, identical on
/// every screen (mirrors the WinUI StatusChip; the tone mapping lives in Core and
/// is tested). Pure display, no commands.
/// </summary>
public partial class StatusChip : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>
    /// Tone name ("Neutral"/"Positive"/"InProgress"/"Error") exposed so the view's
    /// theme-resource DataTriggers can recolor the glyph and label. Kept in sync by
    /// <see cref="RefreshFromStatus"/>.
    /// </summary>
    public static readonly DependencyProperty ToneNameProperty = DependencyProperty.Register(
        nameof(ToneName),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(nameof(StatusTone.Neutral))
    );

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(DocumentStatus),
        typeof(StatusChip),
        new PropertyMetadata(default(DocumentStatus), OnStatusChanged)
    );

    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush),
        typeof(Brush),
        typeof(StatusChip),
        new PropertyMetadata(Brushes.Transparent)
    );

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush),
        typeof(Brush),
        typeof(StatusChip),
        new PropertyMetadata(Brushes.Transparent)
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string ToneName
    {
        get => (string)GetValue(ToneNameProperty);
        set => SetValue(ToneNameProperty, value);
    }

    public DocumentStatus Status
    {
        get => (DocumentStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public StatusChip()
    {
        InitializeComponent();
        RefreshFromStatus(null);
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StatusChip chip)
        {
            return;
        }

        // Auto-label follows Status unless the caller set an explicit Text.
        // A Text matching the previous auto-label (e.g. container recycle
        // with a new row's Status) is treated as auto and updated; a custom
        // Text that never matched an auto-label is preserved.
        var oldLabel = e.OldValue is DocumentStatus oldStatus
            ? DocumentStatusPresentation.LabelFor(oldStatus)
            : DocumentStatusPresentation.LabelFor((string?)null);
        var newStatus = (DocumentStatus)e.NewValue;
        if (string.IsNullOrEmpty(chip.Text) || chip.Text == oldLabel)
        {
            chip.Text = DocumentStatusPresentation.LabelFor(newStatus);
        }

        chip.RefreshFromStatus(newStatus);
    }

    private void RefreshFromStatus(DocumentStatus? status)
    {
        if (string.IsNullOrEmpty(Text) && status is not null)
        {
            Text = DocumentStatusPresentation.LabelFor(status.Value);
        }
        else if (string.IsNullOrEmpty(Text))
        {
            Text = DocumentStatusPresentation.LabelFor((string?)null);
        }

        var tone = status is null
            ? StatusTone.Neutral
            : DocumentStatusPresentation.ToneFor(status.Value);

        // Drive the view triggers by tone name and resolve the semantic brushes
        // for the DotBrush/TextBrush contract; neither uses a color literal.
        ToneName = tone.ToString();
        var brush = ResolveToneBrush(tone);
        DotBrush = brush;
        TextBrush = brush;
    }

    /// <summary>
    /// Resolves the library's semantic brush for a tone from the application theme
    /// resources (Light/Dark/High Contrast aware) instead of a frozen literal.
    /// </summary>
    private static Brush ResolveToneBrush(StatusTone tone)
    {
        var key = tone switch
        {
            StatusTone.Positive => "SystemFillColorSuccessBrush",
            StatusTone.InProgress => "AccentFillColorDefaultBrush",
            StatusTone.Error => "SystemFillColorCriticalBrush",
            _ => "SystemFillColorNeutralBrush",
        };

        return Application.Current?.Resources[key] as Brush ?? Brushes.Transparent;
    }
}
