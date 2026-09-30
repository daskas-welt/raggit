using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Components;

/// <summary>
/// Reusable document-status indicator: themed status glyph + label, identical on
/// every screen (mirrors the WinUI StatusChip; the tone mapping lives in Core and
/// is tested). The tone → colour-role mapping lives here and is published as
/// theme-resource references, so a live theme switch repaints the chip. Pure
/// display, no commands.
/// </summary>
public partial class StatusChip : UserControl
{
    /// <summary>
    /// Whether <see cref="Text"/> is the label this control derives from
    /// <see cref="Status"/>. Only a caller-supplied label survives a later status
    /// change; a derived one is always refreshed, which is what a recycled row
    /// needs when its <see cref="Status"/> is re-bound.
    /// </summary>
    private bool _labelIsDerived = true;

    private bool _writingDerivedLabel;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(string.Empty, OnTextChanged)
    );

    /// <summary>
    /// Tone name ("Neutral"/"Positive"/"InProgress"/"Error") that the view's
    /// <c>DataTrigger</c>s read to select the status glyph's shape. Colour is not
    /// driven from here: the tone's colour roles are published on
    /// <see cref="DotBrush"/> / <see cref="TextBrush"/> so the mapping exists once.
    /// Kept in sync by <see cref="RefreshFromStatus"/>.
    /// </summary>
    public static readonly DependencyProperty ToneNameProperty = DependencyProperty.Register(
        nameof(ToneName),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(nameof(StatusTone.Neutral))
    );

    /// <summary>
    /// The status the chip renders, or <c>null</c> (the default) when no status has
    /// been applied yet — the chip then shows the neutral presentation.
    /// <para>
    /// The default deliberately is not a real <see cref="DocumentStatus"/> value:
    /// WPF raises no property-changed callback when a value equal to the current
    /// one is written, so a default of, say, <c>Uploading</c> would leave a row
    /// bound to a document in that state rendering as neutral, because the
    /// callback that maps status → tone/label would never run.
    /// </para>
    /// </summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(DocumentStatus?),
        typeof(StatusChip),
        new PropertyMetadata(null, OnStatusChanged)
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

    public DocumentStatus? Status
    {
        get => (DocumentStatus?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// Brush the status glyph is painted with, set from the tone's colour role
    /// (<see cref="GlyphBrushKey"/>) as a theme-resource reference.
    /// </summary>
    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    /// <summary>
    /// Brush the status label is painted with, set from the tone's colour role
    /// (<see cref="LabelBrushKey"/>) as a theme-resource reference.
    /// </summary>
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

        chip.RefreshFromStatus(
            e.NewValue is DocumentStatus status ? status : (DocumentStatus?)null
        );
    }

    /// <summary>
    /// Sets the derived label without marking it as caller-supplied. Restoring the
    /// flag keeps the "derived" invariant true on every path that publishes a
    /// derived label, including the recovery branch in
    /// <see cref="RefreshFromStatus"/> that re-derives after a caller cleared it.
    /// </summary>
    private void SetDerivedLabel(string label)
    {
        _writingDerivedLabel = true;
        Text = label;
        _writingDerivedLabel = false;
        _labelIsDerived = true;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusChip chip && !chip._writingDerivedLabel)
        {
            chip._labelIsDerived = false;
        }
    }

    /// <summary>
    /// Applies a status to the chip: the label follows it unless a caller supplied
    /// its own <see cref="Text"/>, and the tone picks the glyph shape and the
    /// colour roles the view binds to.
    /// </summary>
    private void RefreshFromStatus(DocumentStatus? status)
    {
        if (_labelIsDerived || string.IsNullOrEmpty(Text))
        {
            SetDerivedLabel(
                status is null
                    ? DocumentStatusPresentation.LabelFor((string?)null)
                    : DocumentStatusPresentation.LabelFor(status.Value)
            );
        }

        var tone = status is null
            ? StatusTone.Neutral
            : DocumentStatusPresentation.ToneFor(status.Value);

        // Drive the glyph shape from the tone name and publish the tone's colour
        // roles on the DotBrush/TextBrush contract the view binds to, so this
        // mapping exists exactly once (feature 023, T038).
        ToneName = tone.ToString();
        SetResourceReference(DotBrushProperty, GlyphBrushKey(tone));
        SetResourceReference(TextBrushProperty, LabelBrushKey(tone));
    }

    /// <summary>
    /// Glyph colour role per tone: the accent <em>decoration</em> token for the
    /// in-progress state (the same token the client uses for its other emphasis
    /// glyphs) and the semantic status token otherwise. Never the accent
    /// <em>fill</em> token — that one belongs behind text on accent.
    /// </summary>
    private static string GlyphBrushKey(StatusTone tone) =>
        tone switch
        {
            StatusTone.Positive => "SystemFillColorSuccessBrush",
            StatusTone.InProgress => "SystemAccentColorPrimaryBrush",
            StatusTone.Error => "SystemFillColorCriticalBrush",
            _ => "SystemFillColorNeutralBrush",
        };

    /// <summary>
    /// Label colour role per tone: content stops at the primary text level except
    /// where the state itself is the message (completion, failure).
    /// </summary>
    private static string LabelBrushKey(StatusTone tone) =>
        tone switch
        {
            StatusTone.Positive => "SystemFillColorSuccessBrush",
            StatusTone.Error => "SystemFillColorCriticalBrush",
            _ => "TextFillColorPrimaryBrush",
        };
}
