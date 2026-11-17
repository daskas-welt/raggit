using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using RAGGit.Client.WPF.Views.Pages;
using Wpf.Ui.Controls;

namespace RAGGit.Tests.Client;

/// <summary>
/// 034 US3: the Create Document page exposes its controls, stable automation ids
/// and busy-state disabling without relying on colour or pointer-only input.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class DocumentCreationUiTests(WpfAppFixture app)
{
    [Fact]
    public void Page_ExposesKeyboardReachableControlsAndStableIds()
    {
        app.Run(() =>
        {
            var page = new DocumentCreationPage();
            page.Measure(new Size(1280, 900));
            page.Arrange(new Rect(0, 0, 1280, 900));
            page.UpdateLayout();

            FindById<Wpf.Ui.Controls.TextBox>(page, "DocumentCreationDescription")
                .Should()
                .NotBeNull()
                .And.Match<Wpf.Ui.Controls.TextBox>(box =>
                    box.AcceptsReturn && box.Focusable && box.MaxLength == 10_000
                );
            FindVisual<Wpf.Ui.Controls.TextBlock>(page)
                .Should()
                .Contain(block => block.Text.Contains("choose a format"));
            FindById<ComboBox>(page, "DocumentCreationFormat")
                .Should()
                .NotBeNull()
                .And.Match<ComboBox>(box => box.Focusable);
            FindById<Wpf.Ui.Controls.Button>(page, "CreateDocumentButton")
                .Should()
                .NotBeNull()
                .And.Match<Wpf.Ui.Controls.Button>(button => button.Focusable);
            FindById<Wpf.Ui.Controls.Button>(page, "CancelDocumentCreationButton")
                .Should()
                .NotBeNull();
            FindById<ProgressRing>(page, "DocumentCreationProgressRing").Should().NotBeNull();
            FindById<InfoBar>(page, "DocumentCreationStatusInfoBar").Should().NotBeNull();
        });
    }

    [Fact]
    public void Busy_DisablesTheEditableControls()
    {
        app.Run(() =>
        {
            var page = new DocumentCreationPage
            {
                ViewModel =
                {
                    Description = "Draft a welcome note.",
                    SelectedFormat = RAGGit.Core.Models.DocumentOutputFormat.Txt,
                },
            };
            page.ViewModel.IsBusy = true;
            page.Measure(new Size(1280, 900));
            page.Arrange(new Rect(0, 0, 1280, 900));
            page.UpdateLayout();

            FindById<Wpf.Ui.Controls.TextBox>(page, "DocumentCreationDescription")!
                .IsEnabled.Should()
                .BeFalse();
            FindById<ComboBox>(page, "DocumentCreationFormat")!.IsEnabled.Should().BeFalse();
            page.ViewModel.CreateDocumentCommand.CanExecute(null).Should().BeFalse();
        });
    }

    private static T? FindById<T>(DependencyObject root, string automationId)
        where T : DependencyObject =>
        FindVisual<T>(root)
            .FirstOrDefault(element =>
                AutomationProperties.GetAutomationId(element) == automationId
            );

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
