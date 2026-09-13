using System;
using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T004: Verify the User entity matches the 004-identity data model.
/// </summary>
public sealed class UserEntityTests
{
    [Fact]
    public void UserEntity_HasExpectedProperties()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "ada",
            DisplayName = "Ada Lovelace",
            Role = UserRole.Admin,
            PasswordHash = "PBKDF2-SHA256$310000$test",
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = DateTime.UtcNow,
            LastPasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        user.Id.Should().NotBeEmpty();
        user.Username.Should().Be("ada");
        user.DisplayName.Should().Be("Ada Lovelace");
        user.Role.Should().Be(UserRole.Admin);
        user.PasswordHash.Should().StartWith("PBKDF2-SHA256");
        user.IsActive.Should().BeTrue();
        user.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public void UserRole_OnlyAdminAndEmployee()
    {
        Enum.GetNames(typeof(UserRole)).Should().Contain("Admin").And.Contain("Employee");
    }
}
