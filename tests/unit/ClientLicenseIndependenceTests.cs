using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US5/FR-010/SC-003 (carried into 010-winui-client):
/// the client must build and run with no paid/licensed UI component. This
/// static guard fails if any Syncfusion reference or license registration
/// reappears under src/.
/// </summary>
public sealed class ClientLicenseIndependenceTests
{
    private static readonly string[] ForbiddenTokens =
    {
        "Syncfusion",
        "SfListView",
        "SfPullToRefresh",
        "SfAIAssistView",
        "RegisterLicense",
    };

    [Fact]
    public void ClientSources_ContainNoLicensedUiDependency()
    {
        var root = FindRepositoryRoot();
        var srcDir = Path.Combine(root, "src");
        Directory.Exists(srcDir).Should().BeTrue($"src/ should exist under {root}");

        var offenders = new List<string>();
        foreach (var file in EnumerateSources(srcDir))
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add($"{token} in {Path.GetRelativePath(root, file)}");
                }
            }
        }

        offenders.Should().BeEmpty("the client must not depend on a licensed UI component");
    }

    [Fact]
    public void ClientWinUIProject_UsesWindowsAppSdkAndNoMaui()
    {
        var root = FindRepositoryRoot();
        var csproj = Path.Combine(root, "src", "RAGGit.Client.WinUI", "RAGGit.Client.WinUI.csproj");
        File.Exists(csproj).Should().BeTrue();
        var text = File.ReadAllText(csproj);
        text.Should().Contain("Microsoft.WindowsAppSDK");
        text.Should().NotContain("Microsoft.Maui");
        text.Should().NotContain("CommunityToolkit.Maui");
    }

    [Fact]
    public void ClientProjects_ReferenceNoAiOrVectorDependencies()
    {
        var root = FindRepositoryRoot();
        var forbidden = new[]
        {
            "OllamaSharp",
            "LLamaSharp",
            "LanceDB",
            "Qdrant",
            "Microsoft.ML.OnnxRuntime",
            "Microsoft.SemanticKernel",
        };

        foreach (
            var relative in new[]
            {
                Path.Combine("src", "RAGGit.Client.Core", "RAGGit.Client.Core.csproj"),
                Path.Combine("src", "RAGGit.Client.WinUI", "RAGGit.Client.WinUI.csproj"),
            }
        )
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            foreach (var dependency in forbidden)
            {
                text.Should()
                    .NotContain(
                        dependency,
                        $"{Path.GetFileName(relative)} must stay thin (FR-012): no AI/vector dependency"
                    );
            }
        }
    }

    private static IEnumerable<string> EnumerateSources(string srcDir) =>
        Directory
            .EnumerateFiles(srcDir, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                (
                    f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                )
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            );

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
