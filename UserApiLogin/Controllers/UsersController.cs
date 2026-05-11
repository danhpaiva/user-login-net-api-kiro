using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserApiLogin.Data;
using UserApiLogin.DTOs;
using UserApiLogin.Models;

namespace UserApiLogin.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retorna todos os usuários (sem a senha).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _context.Users
            .Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Retorna um usuário pelo Id.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user is null)
            return NotFound(new { message = $"Usuário com Id {id} não encontrado." });

        return Ok(new UserDto { Id = user.Id, Name = user.Name, Email = user.Email });
    }

    /// <summary>
    /// Cria um novo usuário.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RegisterDto dto)
    {
        var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            return Conflict(new { message = "Email já cadastrado." });

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            new UserDto { Id = user.Id, Name = user.Name, Email = user.Email });
    }

    /// <summary>
    /// Atualiza um usuário existente.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RegisterDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        if (user is null)
            return NotFound(new { message = $"Usuário com Id {id} não encontrado." });

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == dto.Email && u.Id != id);

        if (emailExists)
            return Conflict(new { message = "Email já está em uso por outro usuário." });

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        await _context.SaveChangesAsync();

        return Ok(new UserDto { Id = user.Id, Name = user.Name, Email = user.Email });
    }

    /// <summary>
    /// Remove um usuário. Requer autenticação JWT.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user is null)
            return NotFound(new { message = $"Usuário com Id {id} não encontrado." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
