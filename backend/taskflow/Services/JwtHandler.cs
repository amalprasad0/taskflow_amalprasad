using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace taskFlow.Services
{
    public static class JwtHandler
    {
        public static string Secret => Environment.GetEnvironmentVariable("JWT_SECRET") ?? "{JWT_SECRET}";

        public static int TokenExpiryMinutes => int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES") ?? "60");

        public static string GenerateToken(string userId, string email)
        {
            if (string.IsNullOrWhiteSpace(Secret) || Secret == "{JWT_SECRET}")
                throw new InvalidOperationException("JWT secret is not configured. Set JWT_SECRET in environment variables.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(TokenExpiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
