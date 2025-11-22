using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BusinessLayer.Service;

public class TokenService : ITokenService
{
    private readonly JwtOptions _opt;
    public TokenService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public string CreateToken(User user)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        // Lấy tên role từ enum; fallback "System" nếu null
        var roleName = user.Role.RoleName.ToString();

        var claims = new List<Claim>
        {
            // Id trong BaseEntity là string GUID
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),

            new("uid", user.Id),
            new("name", user.UserName ?? string.Empty),
            new("rid", user.RoleId ?? string.Empty),

            // Claim role
            new(ClaimTypes.Role, roleName),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        // Bổ sung claim tuỳ chọn (Google, Avatar) nếu có
        if (!string.IsNullOrWhiteSpace(user.GoogleId))
            claims.Add(new Claim("gid", user.GoogleId));
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
            claims.Add(new Claim("avatar", user.AvatarUrl));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.Now,
            expires: DateTime.Now.AddMinutes(_opt.ExpiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}