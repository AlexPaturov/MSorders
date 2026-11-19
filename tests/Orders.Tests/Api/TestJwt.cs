using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Orders.Tests.Api;

internal static class TestJwt
{
    public const string SigningKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public const string Issuer = "orders-local";
    public const string Audience = "orders-api";
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static string CreateToken(
        Guid? userId = null,
        string? signingKey = null,
        string? issuer = null,
        string? audience = null,
        string? scope = "orders.write",
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var claims = new List<Claim>
        {
            new("sub", (userId ?? UserId).ToString()),
            new("name", "Test User")
        };

        if (scope is not null)
            claims.Add(new Claim("scope", scope));

        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
