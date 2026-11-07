using System.ComponentModel.DataAnnotations;

namespace IdentityService.Dtos;

public record RegisterDto(
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginDto(
    [Required] string Username,
    [Required] string Password);

public record AuthResponse(string Token, string Username);