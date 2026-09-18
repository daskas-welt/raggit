using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RAGGit.Client.Core.Models;

namespace RAGGit.Client.WinUI.Components;

/// <summary>
/// Reusable document-status indicator: colored dot + label, identical on every
/// screen (mirrors the MAUI StatusChip; mapping lives in Core and is tested).
/// </summary>
public sealed partial class StatusChip : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(object),
        typeof(StatusChip),
        new PropertyMetadata(null, OnStatusChanged)
    );

    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush),
        typeof(Brush),
        typeof(StatusChip),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush),
        typeof(Brush),
        typeof(StatusChip),
        new PropertyMetadata(null)
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public object? Status
    {
        get => GetValue(StatusProperty);
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
        ActualThemeChanged += (_, _) => RefreshBrushes();
        RefreshBrushes();
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StatusChip chip)
        {
            return;
        }

        // Auto-label follows Status unless the caller set an explicit Text.
        // A Text matching the previous auto-label (e.g. ListView container
        // recycle with a new row's Status) is treated as auto and updated;
        // a custom Text that never matched an auto-label is preserved.
        var oldLabel = DocumentStatusPresentation.LabelFor(e.OldValue?.ToString());
        if (string.IsNullOrEmpty(chip.Text) || chip.Text == oldLabel)
        {
            chip.Text = DocumentStatusPresentation.LabelFor(e.NewValue?.ToString());
        }

        chip.RefreshBrushes();
    }

    private void RefreshBrushes()
    {
        var tone = DocumentStatusPresentation.ToneFor(Status?.ToString());
        var key = tone switch
        {
            StatusTone.Positive => "StatusPositiveBrush",
            StatusTone.InProgress => "StatusInProgressBrush",
            StatusTone.Error => "StatusErrorBrush",
            _ => "StatusNeutralBrush",
        };

        // Theme-aware: resolves from App.xaml ThemeDictionaries for the current
        // theme (Light/Dark/HighContrast) instead of hardcoded Colors.
        if (
            Application.Current?.Resources.TryGetValue(key, out var brush) == true
            && brush is Brush themed
        )
        {
            DotBrush = themed;
            TextBrush = themed;
            return;
        }

        // Lookup-failure fallback (design-time, unit tests, missing key):
        // prefer the neutral theme brush, else a visible hardcoded gray so the
        // chip never ends up with a null/invisible dot or a stale color.
        if (
            Application.Current?.Resources.TryGetValue("StatusNeutralBrush", out var neutral)
                == true
            && neutral is Brush neutralBrush
        )
        {
            DotBrush = neutralBrush;
            TextBrush = neutralBrush;
            return;
        }

        var gray = new SolidColorBrush(Colors.Gray);
        DotBrush = gray;
        TextBrush = gray;
    }
}
