using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T009: Verify JwtTokenService issues valid HS256 JWTs with the expected claims.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private readonly JwtTokenServiceOptions _options;
    private readonly JwtTokenService _service;

    public JwtTokenServiceTests()
    {
        _options = new JwtTokenServiceOptions { SigningKey = new byte[32], TokenLifetimeHours = 8 };
        _service = new JwtTokenService(Options.Create(_options));
    }

    [Fact]
    public void IssueToken_ReturnsParseableJwtWithCorrectClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "ada",
            DisplayName = "Ada Lovelace",
            Role = UserRole.Admin,
        };

        var token = _service.IssueToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("raggit-workstation");
        jwt.Audiences.Should().Contain("raggit-workstation");
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().ContainSingle(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jwt.Claims.Should().ContainSingle(c => c.Type == "username" && c.Value == "ada");
        jwt.Claims.Should()
            .ContainSingle(c => c.Type == "displayName" && c.Value == "Ada Lovelace");
        (jwt.ValidTo - DateTime.UtcNow)
            .Should()
            .BeCloseTo(TimeSpan.FromHours(8), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "bob",
            DisplayName = "Bob",
            Role = UserRole.Employee,
        };
        var token = _service.IssueToken(user);

        var principal = _service.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void ValidateToken_TamperedToken_Throws()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "carol",
            DisplayName = "Carol",
            Role = UserRole.Employee,
        };
        var token = _service.IssueToken(user) + "x";

        Action act = () => _service.ValidateToken(token);
        act.Should().Throw<SecurityTokenException>();
    }
}
