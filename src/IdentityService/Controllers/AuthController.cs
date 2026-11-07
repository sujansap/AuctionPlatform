using IdentityService.Auth;
using IdentityService.Data;
using IdentityService.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Username, Email = dto.Email };
        var result = await users.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code, e.Description);
            return ValidationProblem(ModelState);
        }

        return new AuthResponse(tokens.CreateToken(user), user.UserName!);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginDto dto)
    {
        var user = await users.FindByNameAsync(dto.Username);

    
        if (user is null || !await users.CheckPasswordAsync(user, dto.Password))
            return Unauthorized("Invalid username or password.");

        return new AuthResponse(tokens.CreateToken(user), user.UserName!);
    }
}