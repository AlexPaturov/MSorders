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

    public static string CreateToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim("sub", UserId.ToString()),
                new Claim("scope", "orders.write"),
                new Claim("name", "Test User")
            },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
