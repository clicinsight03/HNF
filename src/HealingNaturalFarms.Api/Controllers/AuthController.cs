using HealingNaturalFarms.Api.Services;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealingNaturalFarms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> userManager, ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PreferredRegion = request.PreferredRegion
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        var (token, expiresAt) = tokenService.CreateAccessToken(user, roles: []);
        return Ok(new AuthResponse(token, expiresAt, user.Email!, user.FirstName, user.PreferredRegion));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateAccessToken(user, roles);
        return Ok(new AuthResponse(token, expiresAt, user.Email!, user.FirstName, user.PreferredRegion));
    }
}
