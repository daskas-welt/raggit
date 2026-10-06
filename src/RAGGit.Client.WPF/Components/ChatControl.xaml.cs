using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Models;
using RAGGit.Client.Core.Services;

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

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy),
        typeof(bool),
        typeof(ChatControl),
        new PropertyMetadata(false)
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

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>
    /// Focuses the message input — used after clearing the conversation so the
    /// input is immediately ready for the next question (feature 028, U5.3).
    /// </summary>
    public void FocusInput() => InputBox.Focus();

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
        ScrollToNewest();
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Collection updates may arrive off the UI thread; marshal.
        if (Dispatcher.CheckAccess())
        {
            RefreshEmptyState();
            ScrollToNewest();
        }
        else
        {
            Dispatcher.Invoke(() =>
            {
                RefreshEmptyState();
                ScrollToNewest();
            });
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

    private void ScrollToNewest()
    {
        // Public ListBox API only — no template-part lookup. Newest item is
        // last; no-op while empty or before the template is applied.
        if (MessagesList.Items.Count == 0)
        {
            return;
        }

        var newest = MessagesList.Items[MessagesList.Items.Count - 1];
        MessagesList.ScrollIntoView(newest);
        // ScrollIntoView brings the item into view, which for a long answer
        // lands on its tail. A long answer should arrive with its opening
        // visible (029, FR-014), so align the newest item to the top of the
        // viewport once it has been realized.
        Dispatcher.BeginInvoke(
            () =>
            {
                if (
                    MessagesList.ItemContainerGenerator.ContainerFromItem(newest)
                    is FrameworkElement container
                )
                {
                    container.BringIntoView(new Rect(0, 0, container.ActualWidth, 1));
                }
            },
            System.Windows.Threading.DispatcherPriority.Loaded
        );
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Submit the query when Enter is pressed in the chat input.
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
            try
            {
                System.Windows.Clipboard.SetText(message.Text);
                App.Services.GetRequiredService<INotificationService>()
                    .Show("Copied", "Message copied to clipboard.", NotificationKind.Success);
            }
            catch
            {
                // Clipboard may be locked by another process; a failed copy
                // stays silent rather than confirming what did not happen.
            }
        }
    }
}
