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
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, TokenService tokenService, ILogger<AuthController> logger)
    {
        _context      = context;
        _tokenService = tokenService;
        _logger       = logger;
    }

    /// <summary>
    /// Autentica um usuário e retorna um token JWT.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        _logger.LogInformation("Tentativa de login para o email {Email}", dto.Email);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
        {
            _logger.LogWarning("Falha de autenticação para o email {Email} — credenciais inválidas", dto.Email);
            return Unauthorized(new { message = "Email ou senha inválidos." });
        }

        var token = _tokenService.GenerateToken(user);

        _logger.LogInformation("Login bem-sucedido para o usuário {UserId} ({Email})", user.Id, user.Email);

        return Ok(new
        {
            token,
            user = new UserDto
            {
                Id    = user.Id,
                Name  = user.Name,
                Email = user.Email
            }
        });
    }
}
