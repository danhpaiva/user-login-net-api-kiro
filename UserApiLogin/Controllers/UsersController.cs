using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UserApiLogin.Data;
using UserApiLogin.DTOs;
using UserApiLogin.Models;

namespace UserApiLogin.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    // Tempo de vida do cache
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    // Prefixos de chave de cache
    private const string CacheKeyList   = "users_list";
    private const string CacheKeyById   = "users_id_";
    private const string CacheKeyVersion = "users_list_version";

    public UsersController(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    /// <summary>
    /// Retorna os usuários paginados (sem a senha).
    /// </summary>
    /// <param name="page">Número da página (padrão: 1).</param>
    /// <param name="pageSize">Itens por página, entre 1 e 50 (padrão: 10).</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1)
            return BadRequest(new { message = "O parâmetro 'page' deve ser maior que zero." });

        if (pageSize < 1 || pageSize > 50)
            return BadRequest(new { message = "O parâmetro 'pageSize' deve estar entre 1 e 50." });

        var cacheKey = $"{CacheKeyList}_v{GetListVersion()}_p{page}_s{pageSize}";

        if (!_cache.TryGetValue(cacheKey, out PagedResultDto<UserDto>? result))
        {
            var totalItems = await _context.Users.CountAsync();

            var items = await _context.Users
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email })
                .ToListAsync();

            result = new PagedResultDto<UserDto>
            {
                Page      = page,
                PageSize  = pageSize,
                TotalItems = totalItems,
                Items     = items
            };

            _cache.Set(cacheKey, result, CacheDuration);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retorna um usuário pelo Id.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cacheKey = $"{CacheKeyById}{id}";

        if (!_cache.TryGetValue(cacheKey, out UserDto? dto))
        {
            var user = await _context.Users.FindAsync(id);

            if (user is null)
                return NotFound(new { message = $"Usuário com Id {id} não encontrado." });

            dto = new UserDto { Id = user.Id, Name = user.Name, Email = user.Email };

            _cache.Set(cacheKey, dto, CacheDuration);
        }

        return Ok(dto);
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
            Name     = dto.Name,
            Email    = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        InvalidateListCache();

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

        user.Name     = dto.Name;
        user.Email    = dto.Email;
        user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        await _context.SaveChangesAsync();

        _cache.Remove($"{CacheKeyById}{id}");
        InvalidateListCache();

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

        _cache.Remove($"{CacheKeyById}{id}");
        InvalidateListCache();

        return NoContent();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna a versão atual da listagem. Cada incremento invalida todas as
    /// entradas de cache paginadas sem precisar enumerá-las.
    /// </summary>
    private long GetListVersion() =>
        _cache.GetOrCreate(CacheKeyVersion, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 1L;
        });

    /// <summary>
    /// Incrementa a versão da listagem, tornando obsoletas todas as entradas
    /// de cache paginadas existentes.
    /// </summary>
    private void InvalidateListCache()
    {
        var current = _cache.GetOrCreate(CacheKeyVersion, _ => 1L);
        _cache.Set(CacheKeyVersion, current + 1, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }
}
