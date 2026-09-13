using System;
using System.Linq;
using FluentAssertions;
using RAGGit.Core.Auth;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T005: Verify PBKDF2-SHA256 password hashing and fixed-time verification.
/// </summary>
public sealed class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ReturnsSelfDescribingFormat()
    {
        var hash = PasswordHasher.HashPassword("correct horse battery staple");

        hash.Should().StartWith("PBKDF2-SHA256$310000$");
        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        Convert.FromBase64String(parts[2]).Should().HaveCount(16);
        Convert.FromBase64String(parts[3]).Should().HaveCount(32);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.HashPassword("my secure password");

        PasswordHasher.VerifyPassword("my secure password", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.HashPassword("my secure password");

        PasswordHasher.VerifyPassword("wrong password", hash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_UnknownFormat_ReturnsFalse()
    {
        PasswordHasher.VerifyPassword("x", "not-a-hash").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void HashPassword_NullOrEmpty_Throws(string? password)
    {
        Action act = () => PasswordHasher.HashPassword(password!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HashPassword_DoesNotContainPlaintextPassword()
    {
        var password = "correct horse battery staple";
        var hash = PasswordHasher.HashPassword(password);

        hash.Should().NotContain(password);
        hash.Should()
            .NotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)));
    }

    [Fact]
    public void VerifyPassword_MalformedHash_ReturnsFalse()
    {
        PasswordHasher.VerifyPassword("any", "PBKDF2-SHA256$310000$short$short").Should().BeFalse();
        PasswordHasher
            .VerifyPassword(
                "any",
                "PBKDF2-MD5$310000$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
            )
            .Should()
            .BeFalse();
        PasswordHasher.VerifyPassword("any", "not-a-hash").Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_DifferentWrongPasswords_BothFalse()
    {
        var hash = PasswordHasher.HashPassword("my secure password");

        PasswordHasher.VerifyPassword("wrong password", hash).Should().BeFalse();
        PasswordHasher.VerifyPassword("x", hash).Should().BeFalse();
        PasswordHasher.VerifyPassword(new string('a', 200), hash).Should().BeFalse();
    }
}
