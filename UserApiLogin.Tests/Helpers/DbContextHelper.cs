using Microsoft.EntityFrameworkCore;
using UserApiLogin.Data;

namespace UserApiLogin.Tests.Helpers;

/// <summary>
/// Cria um AppDbContext em memória com nome único por teste,
/// evitando interferência entre testes paralelos.
/// </summary>
public static class DbContextHelper
{
    public static AppDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
