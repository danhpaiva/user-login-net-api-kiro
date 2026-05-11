using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UserApiLogin.Controllers;
using UserApiLogin.DTOs;
using UserApiLogin.Models;
using UserApiLogin.Tests.Helpers;

namespace UserApiLogin.Tests.Controllers;

public class UsersControllerTests
{
    private static IMemoryCache CreateCache() =>
        new MemoryCache(new MemoryCacheOptions());

    private UsersController CriarController(string? dbName = null)
    {
        var context = DbContextHelper.CreateInMemoryContext(dbName ?? Guid.NewGuid().ToString());
        return new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_SemUsuarios_DeveRetornarListaVazia()
    {
        var controller = CriarController();

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResultDto<UserDto>>(ok.Value);
        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.TotalItems);
    }

    [Fact]
    public async Task GetAll_ComUsuariosCadastrados_DeveRetornarTodos()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.AddRange(
            new User { Name = "Alice", Email = "alice@email.com", Password = "hash1" },
            new User { Name = "Bob",   Email = "bob@email.com",   Password = "hash2" }
        );
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResultDto<UserDto>>(ok.Value);
        Assert.Equal(2, paged.TotalItems);
        Assert.Equal(2, paged.Items.Count());
    }

    [Fact]
    public async Task GetAll_NaoDeveExporSenha()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Name = "Teste", Email = "t@t.com", Password = "hash_secreto" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResultDto<UserDto>>(ok.Value);
        Assert.All(paged.Items, u => Assert.Null(u.GetType().GetProperty("Password")));
    }

    [Fact]
    public async Task GetAll_ComPaginacao_DeveRetornarApenasItensDaPagina()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        for (var i = 1; i <= 15; i++)
            context.Users.Add(new User { Name = $"User{i}", Email = $"user{i}@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);

        // Página 1 com 10 itens
        var result1 = await controller.GetAll(page: 1, pageSize: 10);
        var ok1 = Assert.IsType<OkObjectResult>(result1);
        var paged1 = Assert.IsType<PagedResultDto<UserDto>>(ok1.Value);
        Assert.Equal(10, paged1.Items.Count());
        Assert.Equal(15, paged1.TotalItems);
        Assert.Equal(2, paged1.TotalPages);
        Assert.True(paged1.HasNextPage);
        Assert.False(paged1.HasPreviousPage);

        // Página 2 com os 5 restantes
        var result2 = await controller.GetAll(page: 2, pageSize: 10);
        var ok2 = Assert.IsType<OkObjectResult>(result2);
        var paged2 = Assert.IsType<PagedResultDto<UserDto>>(ok2.Value);
        Assert.Equal(5, paged2.Items.Count());
        Assert.False(paged2.HasNextPage);
        Assert.True(paged2.HasPreviousPage);
    }

    [Fact]
    public async Task GetAll_ComPageInvalido_DeveRetornarBadRequest()
    {
        var controller = CriarController();

        var result = await controller.GetAll(page: 0);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ComPageSizeInvalido_DeveRetornarBadRequest()
    {
        var controller = CriarController();

        var result = await controller.GetAll(pageSize: 100);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_SegundaChamada_DeveUsarCache()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Name = "Cached", Email = "cached@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var cache = CreateCache();
        var controller = new UsersController(context, cache, NullLogger<UsersController>.Instance);

        // Primeira chamada — popula o cache
        await controller.GetAll();

        // Adiciona usuário diretamente no banco sem invalidar cache
        context.Users.Add(new User { Name = "Novo", Email = "novo@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        // Segunda chamada — deve retornar resultado cacheado (1 item, não 2)
        var result = await controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResultDto<UserDto>>(ok.Value);
        Assert.Equal(1, paged.TotalItems);
    }

    [Fact]
    public async Task Create_DeveInvalidarCacheDeListagem()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var cache = CreateCache();
        var controller = new UsersController(context, cache, NullLogger<UsersController>.Instance);

        // Popula o cache com lista vazia
        await controller.GetAll();

        // Cria um usuário — deve invalidar o cache
        await controller.Create(new RegisterDto { Name = "Novo", Email = "novo@email.com", Password = "senha123" });

        // Próxima chamada deve buscar do banco e retornar 1 item
        var result = await controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResultDto<UserDto>>(ok.Value);
        Assert.Equal(1, paged.TotalItems);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ComIdExistente_DeveRetornarOk()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "Carlos", Email = "carlos@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var result = await controller.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Carlos", dto.Name);
    }

    [Fact]
    public async Task GetById_ComIdInexistente_DeveRetornarNotFound()
    {
        var controller = CriarController();

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetById_SegundaChamada_DeveUsarCache()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "Original", Email = "orig@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var cache = CreateCache();
        var controller = new UsersController(context, cache, NullLogger<UsersController>.Instance);

        // Primeira chamada — popula o cache
        await controller.GetById(1);

        // Altera diretamente no banco sem passar pelo controller
        var user = context.Users.First();
        user.Name = "Alterado";
        await context.SaveChangesAsync();

        // Segunda chamada — deve retornar o valor cacheado
        var result = await controller.GetById(1);
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal("Original", dto.Name);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ComDadosValidos_DeveRetornarCreated()
    {
        var controller = CriarController();
        var dto = new RegisterDto { Name = "Novo", Email = "novo@email.com", Password = "senha123" };

        var result = await controller.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var userDto = Assert.IsType<UserDto>(created.Value);
        Assert.Equal("Novo", userDto.Name);
        Assert.Equal("novo@email.com", userDto.Email);
    }

    [Fact]
    public async Task Create_ComEmailDuplicado_DeveRetornarConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Name = "Existente", Email = "dup@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var dto = new RegisterDto { Name = "Outro", Email = "dup@email.com", Password = "senha123" };

        var result = await controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Create_DeveSalvarSenhaHasheada()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var dto = new RegisterDto { Name = "Hash", Email = "hash@email.com", Password = "senhaPlana" };

        await controller.Create(dto);

        var user = context.Users.First();
        Assert.NotEqual("senhaPlana", user.Password);
        Assert.True(BCrypt.Net.BCrypt.Verify("senhaPlana", user.Password));
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ComIdExistente_DeveRetornarOkComDadosAtualizados()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "Antigo", Email = "antigo@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var dto = new RegisterDto { Name = "Novo Nome", Email = "novo@email.com", Password = "novaSenha" };

        var result = await controller.Update(1, dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var userDto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal("Novo Nome", userDto.Name);
        Assert.Equal("novo@email.com", userDto.Email);
    }

    [Fact]
    public async Task Update_ComIdInexistente_DeveRetornarNotFound()
    {
        var controller = CriarController();
        var dto = new RegisterDto { Name = "X", Email = "x@email.com", Password = "senha" };

        var result = await controller.Update(999, dto);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ComEmailJaUsadoPorOutroUsuario_DeveRetornarConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.AddRange(
            new User { Id = 1, Name = "User1", Email = "user1@email.com", Password = "hash" },
            new User { Id = 2, Name = "User2", Email = "user2@email.com", Password = "hash" }
        );
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var dto = new RegisterDto { Name = "User1 Atualizado", Email = "user2@email.com", Password = "senha" };

        var result = await controller.Update(1, dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Update_ComMesmoEmailDoProprioUsuario_DeveRetornarOk()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "User1", Email = "user1@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var dto = new RegisterDto { Name = "User1 Novo Nome", Email = "user1@email.com", Password = "novaSenha" };

        var result = await controller.Update(1, dto);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ComIdExistente_DeveRetornarNoContent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "Deletar", Email = "del@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, CreateCache(), NullLogger<UsersController>.Instance);
        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, context.Users.Count());
    }

    [Fact]
    public async Task Delete_ComIdInexistente_DeveRetornarNotFound()
    {
        var controller = CriarController();

        var result = await controller.Delete(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
