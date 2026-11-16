using System;
using System.Collections.Generic;
using System.ComponentModel;
using FluentAssertions;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US3: only an owned, cited, non-refusal answer exposes the Save as action.
/// </summary>
public sealed class ChatMessageDocumentTests
{
    [Fact]
    public void CitedOwnedAnswer_CanSave()
    {
        var message = Message("Answer [1]", Guid.NewGuid(), Citation());

        message.CanSave.Should().BeTrue();
    }

    [Fact]
    public void MissingQueryId_CannotSave()
    {
        Message("Answer [1]", null, Citation()).CanSave.Should().BeFalse();
    }

    [Fact]
    public void Refusal_CannotSave()
    {
        Message("no relevant content found", Guid.NewGuid()).CanSave.Should().BeFalse();
    }

    [Fact]
    public void CitationlessAnswer_CannotSave()
    {
        Message("An answer without sources.", Guid.NewGuid()).CanSave.Should().BeFalse();
    }

    [Fact]
    public void SavedPath_RaisesChangeNotification()
    {
        var message = Message("Answer [1]", Guid.NewGuid(), Citation());
        var raised = new List<string?>();
        ((INotifyPropertyChanged)message).PropertyChanged += (_, args) =>
            raised.Add(args.PropertyName);

        message.SavedPath = "C:\\temp\\answer.docx";

        raised.Should().Contain(nameof(ChatMessage.SavedPath));
    }

    private static ChatMessage Message(string text, Guid? queryId, params Citation[] citations) =>
        new()
        {
            Text = text,
            IsUser = false,
            QueryId = queryId,
            Citations = citations,
        };

    private static Citation Citation() =>
        new()
        {
            DocumentId = Guid.NewGuid(),
            ChunkId = Guid.NewGuid(),
            Text = "quote",
            Ordinal = 0,
        };
}
