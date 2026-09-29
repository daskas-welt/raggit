using System;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// Holds the pending query id for <c>QueryDetailPage</c> navigation.
/// WPF-UI's <c>Navigate(Type, dataContext)</c> overload would replace the
/// page DataContext, so the id travels here instead.
/// </summary>
public sealed class QueryDetailNavigationState
{
    public Guid? PendingQueryId { get; set; }
}
