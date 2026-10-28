using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Client.WPF;
using RAGGit.Client.WPF.Views;
using RAGGit.Client.WPF.Views.Dialogs;
using RAGGit.Client.WPF.Views.Pages;

namespace RAGGit.Tests.Client;

/// <summary>
/// Loads every desktop screen for real. A page whose XAML throws during
/// construction is swallowed by NavigationView plus the global
/// DispatcherUnhandledException handler, so the user only sees a nav click that
/// does nothing and a line in crash.log. These tests turn that silence into a
/// failing test that names the screen and the real exception.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class ScreenLoadTests(WpfAppFixture app)
{
    /// <summary>
    /// Every view the shell can put in front of a user: the nine navigable
    /// pages, the shell window, and the three content dialogs.
    /// </summary>
    public static TheoryData<string> Screens =>
        new()
        {
            "DashboardPage",
            "QueryPage",
            "QueryDetailPage",
            "LibraryPage",
            "DocumentsMinePage",
            "HistoryPage",
            "AdminUsersPage",
            "SettingsPage",
            "LoginPage",
            "MainWindow",
            "UploadDialog",
            "CreatePersonDialog",
            "ResetPasswordDialog",
        };

    [Theory]
    [MemberData(nameof(Screens))]
    public void Screen_LoadsAndLaysOutWithoutErrors(string screenName)
    {
        var report = app.Run(() => LoadScreen(screenName));

        report.VisualNodes.Should().BePositive($"{screenName} must build a visual tree");
        report
            .BindingErrors.Should()
            .BeEmpty($"{screenName} must not emit data-binding errors while laying out");
    }

    /// <summary>
    /// Runs on the dispatcher thread. Construction, layout and binding
    /// evaluation all have to happen there: WPF objects are thread-affine.
    /// </summary>
    private static ScreenReport LoadScreen(string screenName)
    {
        var element = Create(screenName);
        var bindingErrors = new List<string>();
        var listener = new CollectingTraceListener(bindingErrors);
        var source = PresentationTraceSources.DataBindingSource;
        var previousLevel = source.Switch.Level;

        try
        {
            source.Listeners.Add(listener);
            source.Switch.Level = SourceLevels.Error | SourceLevels.Warning;

            // Forces templates to expand and bindings to evaluate, which is
            // what a nav click does before the page paints.
            element.Measure(new Size(1280, 900));
            element.Arrange(new Rect(0, 0, 1280, 900));
            element.UpdateLayout();
        }
        finally
        {
            source.Listeners.Remove(listener);
            source.Switch.Level = previousLevel;
        }

        return new ScreenReport(
            CountVisualNodes(element),
            CountLogicalNodes(element),
            bindingErrors
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
        );
    }

    private static FrameworkElement Create(string screenName) =>
        screenName switch
        {
            "DashboardPage" => new DashboardPage(),
            "QueryPage" => new QueryPage(),
            "QueryDetailPage" => new QueryDetailPage(),
            "LibraryPage" => new LibraryPage(),
            "DocumentsMinePage" => new DocumentsMinePage(),
            "HistoryPage" => new HistoryPage(),
            "AdminUsersPage" => new AdminUsersPage(),
            "SettingsPage" => new SettingsPage(),
            "LoginPage" => new LoginPage(),
            "MainWindow" => App.Services.GetRequiredService<MainWindow>(),
            "UploadDialog" => new UploadDialog(),
            "CreatePersonDialog" => new CreatePersonDialog(
                App.Services.GetRequiredService<AdminUsersViewModel>()
            ),
            "ResetPasswordDialog" => new ResetPasswordDialog(
                App.Services.GetRequiredService<AdminUsersViewModel>(),
                new RAGGit.Client.Core.Services.UserAccountDto
                {
                    Id = Guid.NewGuid(),
                    Username = "screen-check",
                    DisplayName = "Screen Check",
                    Role = RAGGit.Core.Models.UserRole.Employee,
                }
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(screenName),
                screenName,
                "Unknown screen"
            ),
        };

    private static int CountVisualNodes(DependencyObject root, int depth = 0)
    {
        if (depth > 64)
        {
            return 0;
        }

        var count = 1;
        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            count += CountVisualNodes(VisualTreeHelper.GetChild(root, i), depth + 1);
        }

        return count;
    }

    private static int CountLogicalNodes(DependencyObject root)
    {
        var count = 1;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            count += CountLogicalNodes(child);
        }

        return count;
    }

    private sealed record ScreenReport(
        int VisualNodes,
        int LogicalNodes,
        IReadOnlyList<string> BindingErrors
    );

    private sealed class CollectingTraceListener(List<string> messages) : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        public override void WriteLine(string? message) => Write(message);
    }
}
