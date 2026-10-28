using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Guards the page-level empty-state surface (023/028 design system). A local or keyed
/// Style TargetType="ui:Card" replaces WPF-UI's implicit Card style; because Card is a plain
/// ContentControl, the card then loses its template and renders as a bare panel with no
/// background, border or padding. These static checks keep every page-level empty state on the
/// shared EmptyStateCard style and keep card styles based on the implicit one.
/// </summary>
public sealed class ClientEmptyStateCardStyleTests
{
    private static readonly XNamespace Ui = XNamespace.Get(
        "http://schemas.lepo.co/wpfui/2022/xaml"
    );
    private static readonly XNamespace Xaml = XNamespace.Get(
        "http://schemas.microsoft.com/winfx/2006/xaml"
    );

    private const string AutomationIdAttribute = "AutomationProperties.AutomationId";

    private static readonly string[] EmptyStateCardIds =
    {
        "DashboardEmptyState",
        "LibraryEmptyState",
        "LibraryNoMatchState",
        "DocumentsMineEmptyState",
        "DocumentsMineNoMatchState",
        "HistoryEmptyState",
        "HistoryNoMatchState",
        "AdminEmptyState",
        "AdminNoMatchState",
    };

    [Fact]
    public void AppResources_DefineSharedEmptyStateCardBasedOnTheImplicitCardStyle()
    {
        var app = Load("src", "RAGGit.Client.WPF", "App.xaml");

        var style = app.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .SingleOrDefault(element =>
                (string?)element.Attribute(Xaml + "Key") == "EmptyStateCard"
            );

        style.Should().NotBeNull("App.xaml must expose the shared EmptyStateCard style");
        ((string?)style!.Attribute("BasedOn"))
            .Should()
            .Be(
                "{StaticResource {x:Type ui:Card}}",
                "a Card style without BasedOn drops the Fluent card template"
            );

        var setters = style
            .Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .ToList();

        setters
            .Select(element => (string?)element.Attribute("Property"))
            .Should()
            .Contain(new[] { "Padding", "MaxWidth", "BorderThickness" });

        setters
            .Single(element => (string?)element.Attribute("Property") == "BorderThickness")
            .Attribute("Value")!
            .Value.Should()
            .Be("0", "page-level empty states carry no border");
    }

    [Fact]
    public void PageCardStyles_KeepTheImplicitCardStyleAsBase()
    {
        foreach (var page in PageDocuments())
        {
            var cardStyles = page
                .Document.Descendants()
                .Where(element => element.Name.LocalName == "Style")
                .Where(element =>
                    ((string?)element.Attribute("TargetType"))?.Contains(
                        "ui:Card",
                        StringComparison.Ordinal
                    ) == true
                );

            foreach (var style in cardStyles)
            {
                ((string?)style.Attribute("BasedOn"))
                    .Should()
                    .NotBeNullOrWhiteSpace(
                        $"{page.FileName} declares a ui:Card style that must be BasedOn the shared or implicit card style"
                    );
            }
        }
    }

    [Fact]
    public void PageLevelEmptyStates_UseTheSharedEmptyStateCard()
    {
        var offenders = new List<string>();
        var discovered = new List<string>();

        foreach (var page in PageDocuments())
        {
            foreach (var card in page.Document.Descendants(Ui + "Card"))
            {
                var automationId = (string?)card.Attribute(AutomationIdAttribute);
                if (automationId is null || !IsEmptyStateId(automationId))
                {
                    continue;
                }

                discovered.Add(automationId);

                var styleReference = CardStyleReference(card);
                if (
                    styleReference is null
                    || !styleReference.Contains("EmptyStateCard", StringComparison.Ordinal)
                )
                {
                    offenders.Add(
                        $"{page.FileName}: {automationId} references '{styleReference ?? "no style"}'"
                    );
                }
            }
        }

        discovered.Should().Contain(EmptyStateCardIds);
        offenders.Should().BeEmpty("every page-level empty state must share one card surface");
    }

    private static bool IsEmptyStateId(string automationId) =>
        automationId.Contains("EmptyState", StringComparison.Ordinal)
        || automationId.Contains("NoMatchState", StringComparison.Ordinal);

    private static string? CardStyleReference(XElement card)
    {
        var styleAttribute = (string?)card.Attribute("Style");
        if (!string.IsNullOrWhiteSpace(styleAttribute))
        {
            return styleAttribute;
        }

        var styleProperty = card.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Card.Style");
        var style = styleProperty
            ?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Style");
        return (string?)style?.Attribute("BasedOn");
    }

    private static IEnumerable<(string FileName, XDocument Document)> PageDocuments() =>
        Directory
            .EnumerateFiles(
                Path.Combine(FindRepositoryRoot(), "src", "RAGGit.Client.WPF", "Views", "Pages"),
                "*.xaml",
                SearchOption.AllDirectories
            )
            .Select(path => (Path.GetFileName(path), XDocument.Load(path)));

    private static XDocument Load(params string[] relativePath) =>
        XDocument.Load(Path.Combine(FindRepositoryRoot(), Path.Combine(relativePath)));

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
