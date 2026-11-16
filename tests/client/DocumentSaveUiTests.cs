using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using FluentAssertions;
using RAGGit.Client.Core.Models;
using RAGGit.Client.WPF.Components;
using RAGGit.Client.WPF.Views.Pages;
using RAGGit.Core.Models;
using Wpf.Ui.Controls;

namespace RAGGit.Tests.Client;

/// <summary>
/// 033 US3: the Save as affordance is present on a cited answer in Ask and on a
/// reopened answer in Query Detail, with stable automation ids.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class DocumentSaveUiTests(WpfAppFixture app)
{
    [Fact]
    public void AskMessage_WithCitations_ShowsSaveAsButton()
    {
        app.Run(() =>
        {
            var message = Message();
            var control = new ChatControl { Messages = new[] { message } };
            control.Measure(new Size(900, 700));
            control.Arrange(new Rect(0, 0, 900, 700));
            control.UpdateLayout();

            var button = FindVisual<DropDownButton>(control)
                .FirstOrDefault(candidate =>
                    AutomationProperties.GetAutomationId(candidate)
                    == $"SaveAsButton_{message.AutomationKey}"
                );

            button.Should().NotBeNull();
            button!.Visibility.Should().Be(Visibility.Visible);
        });
    }

    [Fact]
    public void QueryDetail_WithCitedAnswer_ShowsSaveAsButton()
    {
        app.Run(() =>
        {
            var page = new QueryDetailPage
            {
                ViewModel =
                {
                    Detail = new QueryDetail
                    {
                        Id = Guid.NewGuid(),
                        Prompt = "Create a Word document about the policy.",
                        Answer = "The policy allows returns [1].",
                        Citations = new List<HistoryCitation>
                        {
                            new()
                            {
                                DocumentId = Guid.NewGuid(),
                                ChunkId = Guid.NewGuid(),
                                Text = "quote",
                                Ordinal = 0,
                            },
                        },
                        LatencyMs = 1,
                        CreatedAt = DateTime.UtcNow,
                    },
                },
            };
            page.Measure(new Size(1280, 900));
            page.Arrange(new Rect(0, 0, 1280, 900));
            page.UpdateLayout();

            var button = FindVisual<DropDownButton>(page)
                .FirstOrDefault(candidate =>
                    AutomationProperties.GetAutomationId(candidate) == "DetailSaveAsButton"
                );

            button.Should().NotBeNull();
            button!.Visibility.Should().Be(Visibility.Visible);
        });
    }

    private static ChatMessage Message() =>
        new()
        {
            Text = "The policy allows returns [1].",
            IsUser = false,
            QueryId = Guid.NewGuid(),
            Citations = new[]
            {
                new Citation
                {
                    DocumentId = Guid.NewGuid(),
                    ChunkId = Guid.NewGuid(),
                    Text = "quote",
                    Ordinal = 0,
                },
            },
        };

    private static IEnumerable<T> FindVisual<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisual<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
