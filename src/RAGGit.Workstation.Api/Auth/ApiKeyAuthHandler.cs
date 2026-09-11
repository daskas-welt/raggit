using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RAGGit.Workstation.Api.Auth;

/// <summary>
/// Options for API-key authentication.
/// </summary>
public class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";

    /// <summary>
    /// Header name used to transmit the API key. Defaults to <c>X-Api-Key</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Key that grants the Admin role.
    /// </summary>
    public string AdminApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Key that grants the Employee role.
    /// </summary>
    public string EmployeeApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Authentication handler that validates <c>X-Api-Key</c> and maps the key to
/// either the Admin or Employee role per FR-003. Endpoints decorated with
/// <c>[AllowAnonymous]</c> (e.g. <c>/health</c>) bypass this handler.
/// </summary>
public sealed class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return Task.FromResult(
                AuthenticateResult.Fail($"Missing {Options.HeaderName} header.")
            );
        }

        var apiKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(
                AuthenticateResult.Fail($"Missing {Options.HeaderName} header.")
            );
        }

        Claim[] claims;
        if (apiKey == Options.AdminApiKey && !string.IsNullOrEmpty(Options.AdminApiKey))
        {
            claims = [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin")];
        }
        else if (apiKey == Options.EmployeeApiKey && !string.IsNullOrEmpty(Options.EmployeeApiKey))
        {
            claims =
            [
                new Claim(ClaimTypes.Name, "employee"),
                new Claim(ClaimTypes.Role, "Employee"),
            ];
        }
        else
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.ContentType = "application/json";
        await Response.WriteAsync("{\"error\":\"unauthorized\"}");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        Response.ContentType = "application/json";
        await Response.WriteAsync("{\"error\":\"forbidden\"}");
    }
}
