using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace RAGGit.Tests.Client;

/// <summary>
/// Checks every screen's XAML against the WPF-UI traps that compile but fail or
/// render wrong at run time. The member lists come from reflection over the
/// loaded assemblies, so they stay correct when WPF-UI is upgraded — unlike a
/// hard-coded list copied from documentation.
/// </summary>
public sealed class XamlTrapTests
{
    private static readonly XNamespace Ui = XNamespace.Get(
        "http://schemas.lepo.co/wpfui/2022/xaml"
    );
    private static readonly XNamespace Presentation = XNamespace.Get(
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    );

    public static TheoryData<string> XamlFiles
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var file in EnumerateXaml())
            {
                data.Add(Path.GetFileName(file));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void ThemeResourceKeys_AreRealEnumMembers(string fileName)
    {
        var invalid = new List<string>();
        foreach (var value in AttributeValues(fileName))
        {
            foreach (var key in ExtractMarkupKeys(value, "ui:ThemeResource"))
            {
                if (!Enum.TryParse<ThemeResource>(key, ignoreCase: false, out _))
                {
                    invalid.Add(key);
                }
            }
        }

        invalid
            .Distinct(StringComparer.Ordinal)
            .Should()
            .BeEmpty(
                $"{fileName} uses {{ui:ThemeResource}} keys that are not ThemeResource members; "
                    + "an unknown key throws XamlParseException when the page loads"
            );
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void SymbolNames_AreRealSymbolRegularMembers(string fileName)
    {
        var invalid = new List<string>();
        foreach (var element in Elements(fileName))
        {
            var symbol = element.Attribute("Symbol")?.Value;
            if (symbol is null)
            {
                continue;
            }

            if (!Enum.TryParse<SymbolRegular>(symbol, ignoreCase: false, out _))
            {
                invalid.Add($"{element.Name.LocalName}.Symbol=\"{symbol}\"");
            }
        }

        invalid
            .Distinct(StringComparer.Ordinal)
            .Should()
            .BeEmpty($"{fileName} references SymbolRegular members that do not exist");
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void Buttons_UseTheIconPropertyElement_NotTheAttribute(string fileName)
    {
        var offenders = Elements(fileName)
            .Where(e => e.Name == Ui + "Button" && e.Attribute("Icon") is not null)
            .Select(e => e.Attribute("Icon")!.Value)
            .ToList();

        offenders.Should().BeEmpty($"{fileName} sets Icon= on ui:Button, which renders nothing");
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void Appearance_IsNotSetOnControlsWithoutTheProperty(string fileName)
    {
        var offenders = Elements(fileName)
            .Where(e =>
                (e.Name == Ui + "SymbolIcon" || e.Name == Presentation + "TextBlock")
                && e.Attribute("Appearance") is not null
            )
            .Select(e => $"{e.Name.LocalName}.Appearance=\"{e.Attribute("Appearance")!.Value}\"")
            .ToList();

        offenders
            .Should()
            .BeEmpty($"{fileName} sets Appearance where the control has no such property");
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void ItemsSource_DoesNotBindDirectlyToAStaticResource(string fileName)
    {
        var offenders = Elements(fileName)
            .Select(e => e.Attribute("ItemsSource")?.Value)
            .Where(value =>
                value is not null && value.StartsWith("{StaticResource", StringComparison.Ordinal)
            )
            .ToList();

        offenders
            .Should()
            .BeEmpty(
                $"{fileName} assigns a StaticResource straight to ItemsSource; an ObjectDataProvider "
                    + "there throws ArgumentException at load. Bind through it instead: "
                    + "ItemsSource=\"{{Binding Source={{StaticResource ...}}}}\""
            );
    }

    private static IReadOnlyList<XElement> Elements(string fileName) =>
        XDocument.Load(PathFor(fileName)).Descendants().ToList();

    private static IEnumerable<string> AttributeValues(string fileName) =>
        Elements(fileName).SelectMany(e => e.Attributes()).Select(a => a.Value);

    private static IEnumerable<string> ExtractMarkupKeys(string value, string markupName)
    {
        var index = 0;
        var open = "{" + markupName;
        while (true)
        {
            index = value.IndexOf(open, index, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var start = index + open.Length;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            var end = start;
            while (end < value.Length && (char.IsLetterOrDigit(value[end]) || value[end] == '_'))
            {
                end++;
            }

            if (end > start)
            {
                yield return value[start..end];
            }

            index = end > index ? end : index + open.Length;
        }
    }

    private static IEnumerable<string> EnumerateXaml() =>
        Directory
            .EnumerateFiles(
                Path.Combine(RepoRoot(), "src", "RAGGit.Client.WPF"),
                "*.xaml",
                SearchOption.AllDirectories
            )
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string PathFor(string fileName) =>
        EnumerateXaml().First(path => Path.GetFileName(path) == fileName);

    private static string RepoRoot()
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

        throw new InvalidOperationException("Could not locate the repository root (RAGGit.sln).");
    }
}
