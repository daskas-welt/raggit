using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using RAGGit.Client.Maui.Config;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class ClientConfigTests
{
    [Fact]
    public void MissingWorkstationUrl_ReturnsConfigErrorAndNoHttpAttempt()
    {
        var config = new Dictionary<string, string?>
        {
            ["Workstation:ApiKey"] = "some-key",
            ["Api:AdminKey"] = "admin-key",
        };
        var result = ClientConfigResolver.Resolve(config);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Workstation:Url");
        result.HttpAttempted.Should().BeFalse();
        result.WorkstationUrl.Should().BeNull();
    }

    [Fact]
    public void MissingApiKey_ReturnsConfigErrorAndNoHttpAttempt()
    {
        var config = new Dictionary<string, string?>
        {
            ["Workstation:Url"] = "http://localhost:5001",
        };
        var result = ClientConfigResolver.Resolve(config);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("ApiKey");
        result.HttpAttempted.Should().BeFalse();
        result.ApiKey.Should().BeNull();
    }

    [Fact]
    public void MissingBoth_UrlAndKey_ReturnsConfigError()
    {
        var config = new Dictionary<string, string?>();
        var result = ClientConfigResolver.Resolve(config);
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WorkstationApiKey_PrecedesRoleKeys()
    {
        var config = new Dictionary<string, string?>
        {
            ["Workstation:Url"] = "http://localhost:5001",
            ["Workstation:ApiKey"] = "workstation-key",
            ["Api:AdminKey"] = "admin-key",
            ["Api:EmployeeKey"] = "employee-key",
        };
        var resolved = ClientConfigResolver.ResolveApiKey(config);
        resolved.Should().Be("workstation-key");
    }

    [Fact]
    public void FallbackToRoleKeys_WhenWorkstationApiKeyMissing()
    {
        var configAdmin = new Dictionary<string, string?>
        {
            ["Api:AdminKey"] = "admin-key",
            ["Api:EmployeeKey"] = "employee-key",
        };
        ClientConfigResolver.ResolveApiKey(configAdmin).Should().Be("admin-key");

        var configEmployee = new Dictionary<string, string?>
        {
            ["Api:EmployeeKey"] = "employee-key",
        };
        ClientConfigResolver.ResolveApiKey(configEmployee).Should().Be("employee-key");
    }

    [Fact]
    public void ValidConfig_WithFallbackKey_IsValid()
    {
        var config = new Dictionary<string, string?>
        {
            ["Workstation:Url"] = "http://localhost:5001",
            ["Api:EmployeeKey"] = "employee-key",
        };
        var result = ClientConfigResolver.Resolve(config);
        result.IsValid.Should().BeTrue();
        result.WorkstationUrl.Should().Be("http://localhost:5001");
        result.ApiKey.Should().Be("employee-key");
    }

    [Fact]
    public void NoHardcodedUrlOrKeyLiterals_InClientWinUISource()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "RAGGit.Client.WinUI"
        );
        // Normalize for test run location (bin/Debug/net8.0)
        if (!Directory.Exists(root))
            root = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "RAGGit.Client.WinUI"
                )
            );

        Directory.Exists(root).Should().BeTrue($"client source root should exist at {root}");

        var disallowed = new[]
        {
            "http://localhost:5001",
            "http://ai-workstation",
            "dev-admin-key",
            "dev-employee-key",
        };
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            // Allow comments and config file literals? Only check .cs for hard-coded production URLs/keys
            if (file.EndsWith("ClientConfig.cs"))
                continue; // stub / config resolver itself may contain example literals in tests
            foreach (var lit in disallowed)
            {
                text.Should()
                    .NotContain(
                        lit,
                        $"file {Path.GetFileName(file)} should not hard-code {lit} (FR-003)"
                    );
            }
        }

        // Also verify resolver reports no hard-coding
        ClientConfigResolver.HasHardcodedUrlOrKey(root).Should().BeFalse();
    }
}
