using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T002: Verify Auth:* defaults are present in appsettings.json.
/// </summary>
public sealed class AuthOptionsTests
{
    private readonly IConfiguration _config;

    public AuthOptionsTests()
    {
        var basePath = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "RAGGit.Workstation.Api"
            )
        );
        _config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    [Fact]
    public void AuthDefaults_AreConfigured()
    {
        _config["Auth:JwtSigningKeyPath"].Should().Be("./data/auth.key");
        _config.GetValue<int>("Auth:TokenLifetimeHours").Should().Be(8);
        _config.GetValue<int>("Auth:Pbkdf2Iterations").Should().Be(310000);
        _config.GetValue<int>("Auth:LockoutThreshold").Should().Be(5);
        _config.GetValue<int>("Auth:LockoutMinutes").Should().Be(15);
    }
}
