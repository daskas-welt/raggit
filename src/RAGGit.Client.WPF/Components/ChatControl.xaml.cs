using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAGGit.Client.Core.Models;

namespace RAGGit.Client.WPF.Components;

/// <summary>Chat UI with scrollable messages and fixed bottom input.</summary>
public partial class ChatControl : UserControl
{
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(
        nameof(Messages),
        typeof(IEnumerable<ChatMessage>),
        typeof(ChatControl),
        new PropertyMetadata(null, OnMessagesChanged)
    );

    public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(
        nameof(InputText),
        typeof(string),
        typeof(ChatControl),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty SendCommandProperty = DependencyProperty.Register(
        nameof(SendCommand),
        typeof(ICommand),
        typeof(ChatControl),
        new PropertyMetadata(null)
    );

    public IEnumerable<ChatMessage>? Messages
    {
        get => (IEnumerable<ChatMessage>?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public string InputText
    {
        get => (string)GetValue(InputTextProperty);
        set => SetValue(InputTextProperty, value);
    }

    public ICommand? SendCommand
    {
        get => (ICommand?)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    private INotifyCollectionChanged? _trackedMessages;

    public ChatControl()
    {
        InitializeComponent();
        UpdateEmptyState();
    }

    private static void OnMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChatControl control)
        {
            control.UpdateEmptyState();
        }
    }

    private void UpdateEmptyState()
    {
        if (_trackedMessages is not null)
        {
            _trackedMessages.CollectionChanged -= OnMessagesCollectionChanged;
            _trackedMessages = null;
        }

        if (Messages is INotifyCollectionChanged observable)
        {
            _trackedMessages = observable;
            _trackedMessages.CollectionChanged += OnMessagesCollectionChanged;
        }

        RefreshEmptyState();
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Collection updates may arrive off the UI thread; marshal.
        if (Dispatcher.CheckAccess())
        {
            RefreshEmptyState();
        }
        else
        {
            Dispatcher.Invoke(RefreshEmptyState);
        }
    }

    private void RefreshEmptyState()
    {
        var count = Messages switch
        {
            ICollection col => col.Count,
            null => 0,
            _ => 1,
        };
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Mirrors the MAUI Entry ReturnCommand.
        if (e.Key == Key.Enter && SendCommand?.CanExecute(null) == true)
        {
            SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CopyMessageButton_Click(object sender, RoutedEventArgs e)
    {
        if (
            (sender as FrameworkElement)?.DataContext is ChatMessage message
            && !string.IsNullOrEmpty(message.Text)
        )
        {
            System.Windows.Clipboard.SetText(message.Text);
        }
    }
}
