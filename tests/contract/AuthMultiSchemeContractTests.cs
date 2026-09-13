using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T008: Verify both ApiKey and Bearer (JwtBearer) authentication schemes are registered.
/// </summary>
public sealed class AuthMultiSchemeContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthMultiSchemeContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticationSchemes_ContainApiKeyAndBearer()
    {
        var provider = _factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await provider.GetAllSchemesAsync();
        var names = schemes.Select(s => s.Name).ToList();

        names.Should().Contain("ApiKey");
        names.Should().Contain("Bearer");
    }
}
