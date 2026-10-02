using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Components;

/// <summary>
/// Shared status/error footer (feature 028): one severity-styled
/// <see cref="InfoBar"/> plus an optional retry action. Replaces the four
/// per-page status idioms; every asynchronously loading page pairs it with a
/// retry command. Rendering only — all state lives in the bound ViewModel.
/// </summary>
public partial class StatusFooterControl : UserControl
{
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(InfoBarSeverity),
        typeof(StatusFooterControl),
        new PropertyMetadata(InfoBarSeverity.Informational)
    );

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(StatusFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(StatusFooterControl),
        new PropertyMetadata(false)
    );

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand),
        typeof(ICommand),
        typeof(StatusFooterControl),
        new PropertyMetadata(null)
    );

    public static readonly DependencyProperty RetryContentProperty = DependencyProperty.Register(
        nameof(RetryContent),
        typeof(object),
        typeof(StatusFooterControl),
        new PropertyMetadata("Retry")
    );

    public static readonly DependencyProperty StatusAutomationIdProperty =
        DependencyProperty.Register(
            nameof(StatusAutomationId),
            typeof(string),
            typeof(StatusFooterControl),
            new PropertyMetadata(null)
        );

    public static readonly DependencyProperty RetryAutomationIdProperty =
        DependencyProperty.Register(
            nameof(RetryAutomationId),
            typeof(string),
            typeof(StatusFooterControl),
            new PropertyMetadata(null)
        );

    public InfoBarSeverity Severity
    {
        get => (InfoBarSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public object? RetryContent
    {
        get => GetValue(RetryContentProperty);
        set => SetValue(RetryContentProperty, value);
    }

    /// <summary>
    /// Automation id forwarded to the inner status bar (e.g. the page's
    /// existing <c>*StatusBar</c> id), or null for none.
    /// </summary>
    public string? StatusAutomationId
    {
        get => (string?)GetValue(StatusAutomationIdProperty);
        set => SetValue(StatusAutomationIdProperty, value);
    }

    /// <summary>
    /// Automation id forwarded to the inner retry button (e.g.
    /// <c>HistoryRetryButton</c>), or null for none.
    /// </summary>
    public string? RetryAutomationId
    {
        get => (string?)GetValue(RetryAutomationIdProperty);
        set => SetValue(RetryAutomationIdProperty, value);
    }

    public StatusFooterControl()
    {
        InitializeComponent();
    }
}
