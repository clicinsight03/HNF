using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HealingNaturalFarms.Infrastructure.Identity;
using Microsoft.IdentityModel.Tokens;

namespace HealingNaturalFarms.Api.Services;

public interface ITokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user, IList<string> roles);
}

public class TokenService(IConfiguration configuration) : ITokenService
{
    public (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user, IList<string> roles)
    {
        var jwt = configuration.GetSection("Jwt");
        var signingKey = jwt["SigningKey"]!;
        var lifetimeMinutes = jwt.GetValue("AccessTokenLifetimeMinutes", 60);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("preferred_region", user.PreferredRegion.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
