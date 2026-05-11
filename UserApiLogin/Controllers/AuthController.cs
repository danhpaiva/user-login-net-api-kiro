using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UserApiLogin.Data;
using UserApiLogin.DTOs;
using UserApiLogin.Services;

namespace UserApiLogin.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Autentica um usuário e retorna um token JWT.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
            return Unauthorized(new { message = "Email ou senha inválidos." });

        var token = _tokenService.GenerateToken(user);

        return Ok(new
        {
            token,
            user = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            }
        });
    }
}
