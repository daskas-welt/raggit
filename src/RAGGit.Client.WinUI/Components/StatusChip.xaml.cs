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
        new PropertyMetadata(new SolidColorBrush(Colors.Gray))
    );

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush),
        typeof(Brush),
        typeof(StatusChip),
        new PropertyMetadata(new SolidColorBrush(Colors.Gray))
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
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StatusChip chip)
        {
            return;
        }

        var status = e.NewValue?.ToString();
        var color = DocumentStatusPresentation.ToneFor(status) switch
        {
            StatusTone.Positive => Colors.Green,
            StatusTone.InProgress => Colors.Orange,
            StatusTone.Error => Colors.Red,
            _ => Colors.Gray,
        };

        chip.DotBrush = new SolidColorBrush(color);
        chip.TextBrush = new SolidColorBrush(color);

        if (string.IsNullOrEmpty(chip.Text))
        {
            chip.Text = DocumentStatusPresentation.LabelFor(status);
        }
    }
}
