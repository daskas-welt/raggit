using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Locks the shell's startup presentation. The workstation client opens maximized so a launched
/// session uses the whole work area instead of a window that has to be maximized by hand.
/// </summary>
public sealed class ClientMainWindowStartupTests
{
    [Fact]
    public void MainWindow_StartsMaximized()
    {
        var window = XDocument
            .Load(
                Path.Combine(
                    FindRepositoryRoot(),
                    "src",
                    "RAGGit.Client.WPF",
                    "Views",
                    "MainWindow.xaml"
                )
            )
            .Root!;

        ((string?)window.Attribute("WindowState"))
            .Should()
            .Be("Maximized", "the desktop client opens maximized on startup");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RAGGit.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate RAGGit.sln from test base directory."
        );
    }
}
