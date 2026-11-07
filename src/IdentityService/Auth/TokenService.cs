using System.Security.Claims;
using System.Text;
using IdentityService.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public string CreateToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id),
                new Claim("name", user.UserName!)
            }),
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}