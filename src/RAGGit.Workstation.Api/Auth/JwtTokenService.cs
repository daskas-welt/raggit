using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RAGGit.Core.Models;

namespace RAGGit.Workstation.Api.Auth;

/// <summary>
/// Options for <see cref="JwtTokenService"/>.
/// </summary>
public sealed class JwtTokenServiceOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// 256-bit HMAC signing key.
    /// </summary>
    public byte[] SigningKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Token lifetime in hours. Defaults to 8.
    /// </summary>
    public int TokenLifetimeHours { get; set; } = 8;
}

/// <summary>
/// Issues and validates workstation-signed HS256 JWTs for local per-person sessions.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtTokenServiceOptions _options;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IOptions<JwtTokenServiceOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (_options.SigningKey.Length < 32)
            throw new ArgumentException("Signing key must be at least 256 bits.", nameof(options));

        var key = new SymmetricSecurityKey(_options.SigningKey);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "raggit-workstation",
            ValidAudience = "raggit-workstation",
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    }

    /// <summary>
    /// Token lifetime in hours for callers that need <c>expires_in</c> calculations.
    /// </summary>
    public int TokenLifetimeHours => _options.TokenLifetimeHours;

    /// <summary>
    /// Issues an 8-hour JWT identifying the person.
    /// </summary>
    public string IssueToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = new SymmetricSecurityKey(_options.SigningKey);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("username", user.Username),
            new Claim("displayName", user.DisplayName),
        };

        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: "raggit-workstation",
            audience: "raggit-workstation",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.TokenLifetimeHours),
            signingCredentials: credentials
        );

        return handler.WriteToken(token);
    }

    /// <summary>
    /// Validates a token and returns the principal. Throws on invalid signature or expiry.
    /// </summary>
    public ClaimsPrincipal ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, _validationParameters, out _);
    }
}
