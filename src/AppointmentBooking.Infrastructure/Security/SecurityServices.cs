using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppointmentBooking.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "appointment-booking";
    public string Audience { get; set; } = "appointment-booking-clients";

    /// <summary>HMAC signing key. Must be at least 32 characters; supply it via configuration/secrets, never source control.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}

internal sealed class PasswordHasherAdapter : Application.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    private static readonly User Placeholder = User.Create("placeholder@local", "placeholder", "x", UserRole.Customer).Value;

    public string Hash(string password) => _inner.HashPassword(Placeholder, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(Placeholder, hash, password) != PasswordVerificationResult.Failed;
}

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Generate(User user)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(_options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
