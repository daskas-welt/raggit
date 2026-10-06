using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAGGit.Client.Core.Models;

namespace RAGGit.Client.WPF.Components;

/// <summary>
/// Selects the footer template: current page (highlighted), other page
/// numbers, or ellipsis. The current page gets its own template so it is visually
/// distinct from the other page numbers.
/// </summary>
public sealed class PageTokenTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CurrentPageTemplate { get; set; }

    public DataTemplate? NumberTemplate { get; set; }

    public DataTemplate? EllipsisTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container) =>
        item switch
        {
            PageNumberToken number when number.IsCurrent => CurrentPageTemplate!,
            PageNumberToken => NumberTemplate!,
            _ => EllipsisTemplate!,
        };
}

/// <summary>
/// Reusable library-paging footer: status text plus First/Previous/numbered/
/// Next/Last controls. Rendering only — all state lives in the ViewModel.
/// </summary>
public partial class PaginationFooterControl : UserControl
{
    public static readonly DependencyProperty PageSizeOptionsProperty = DependencyProperty.Register(
        nameof(PageSizeOptions),
        typeof(int[]),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty SelectedPageSizeProperty =
        DependencyProperty.Register(
            nameof(SelectedPageSize),
            typeof(int),
            typeof(PaginationFooterControl),
            new PropertyMetadata(10)
        );

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(PaginationFooterControl),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty SequenceProperty = DependencyProperty.Register(
        nameof(Sequence),
        typeof(IReadOnlyList<PageToken>),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty GoToCommandProperty = DependencyProperty.Register(
        nameof(GoToCommand),
        typeof(ICommand),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty FirstCommandProperty = DependencyProperty.Register(
        nameof(FirstCommand),
        typeof(ICommand),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty PreviousCommandProperty = DependencyProperty.Register(
        nameof(PreviousCommand),
        typeof(ICommand),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty NextCommandProperty = DependencyProperty.Register(
        nameof(NextCommand),
        typeof(ICommand),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty LastCommandProperty = DependencyProperty.Register(
        nameof(LastCommand),
        typeof(ICommand),
        typeof(PaginationFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty CanGoPreviousProperty = DependencyProperty.Register(
        nameof(CanGoPrevious),
        typeof(bool),
        typeof(PaginationFooterControl),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty CanGoNextProperty = DependencyProperty.Register(
        nameof(CanGoNext),
        typeof(bool),
        typeof(PaginationFooterControl),
        new PropertyMetadata(false)
    );

    public int[]? PageSizeOptions
    {
        get => (int[]?)GetValue(PageSizeOptionsProperty);
        set => SetValue(PageSizeOptionsProperty, value);
    }

    public int SelectedPageSize
    {
        get => (int)GetValue(SelectedPageSizeProperty);
        set => SetValue(SelectedPageSizeProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public IReadOnlyList<PageToken>? Sequence
    {
        get => (IReadOnlyList<PageToken>?)GetValue(SequenceProperty);
        set => SetValue(SequenceProperty, value);
    }

    public ICommand? GoToCommand
    {
        get => (ICommand?)GetValue(GoToCommandProperty);
        set => SetValue(GoToCommandProperty, value);
    }

    public ICommand? FirstCommand
    {
        get => (ICommand?)GetValue(FirstCommandProperty);
        set => SetValue(FirstCommandProperty, value);
    }

    public ICommand? PreviousCommand
    {
        get => (ICommand?)GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public ICommand? NextCommand
    {
        get => (ICommand?)GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public ICommand? LastCommand
    {
        get => (ICommand?)GetValue(LastCommandProperty);
        set => SetValue(LastCommandProperty, value);
    }

    public bool CanGoPrevious
    {
        get => (bool)GetValue(CanGoPreviousProperty);
        set => SetValue(CanGoPreviousProperty, value);
    }

    public bool CanGoNext
    {
        get => (bool)GetValue(CanGoNextProperty);
        set => SetValue(CanGoNextProperty, value);
    }

    public PaginationFooterControl()
    {
        InitializeComponent();
    }
}
