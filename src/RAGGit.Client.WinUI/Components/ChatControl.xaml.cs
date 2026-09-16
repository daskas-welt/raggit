using System.Collections;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace RAGGit.Client.WinUI.Components;

/// <summary>Chat UI with scrollable messages and fixed bottom input.</summary>
public sealed partial class ChatControl : UserControl
{
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(
        nameof(Messages),
        typeof(IEnumerable),
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

    public IEnumerable? Messages
    {
        get => (IEnumerable?)GetValue(MessagesProperty);
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

    private System.Collections.Specialized.INotifyCollectionChanged? _trackedMessages;

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

        if (Messages is System.Collections.Specialized.INotifyCollectionChanged observable)
        {
            _trackedMessages = observable;
            _trackedMessages.CollectionChanged += OnMessagesCollectionChanged;
        }

        RefreshEmptyState();
    }

    private void OnMessagesCollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e
    )
    {
        // Collection updates may arrive off the UI thread; marshal.
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshEmptyState();
        }
        else
        {
            DispatcherQueue.TryEnqueue(RefreshEmptyState);
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

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Mirrors the MAUI Entry ReturnCommand.
        if (e.Key == Windows.System.VirtualKey.Enter && SendCommand?.CanExecute(null) == true)
        {
            SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
