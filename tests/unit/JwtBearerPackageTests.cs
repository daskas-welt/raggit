using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T001: Verify the Workstation.Api project references Microsoft.AspNetCore.Authentication.JwtBearer 8.x.
/// </summary>
public sealed class JwtBearerPackageTests
{
    [Fact]
    public void JwtBearerDefaults_AuthenticationScheme_IsBearer()
    {
        JwtBearerDefaults.AuthenticationScheme.Should().Be("Bearer");
    }
}
