using Microsoft.AspNetCore.Mvc;
using UserApiLogin.Controllers;
using UserApiLogin.DTOs;
using UserApiLogin.Models;
using UserApiLogin.Tests.Helpers;

namespace UserApiLogin.Tests.Controllers;

public class UsersControllerTests
{
    private UsersController CriarController(string? dbName = null)
    {
        var context = DbContextHelper.CreateInMemoryContext(dbName ?? Guid.NewGuid().ToString());
        return new UsersController(context);
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_SemUsuarios_DeveRetornarListaVazia()
    {
        var controller = CriarController();

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var lista = Assert.IsAssignableFrom<IEnumerable<UserDto>>(ok.Value);
        Assert.Empty(lista);
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

        var controller = new UsersController(context);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var lista = Assert.IsAssignableFrom<IEnumerable<UserDto>>(ok.Value);
        Assert.Equal(2, lista.Count());
    }

    [Fact]
    public async Task GetAll_NaoDeveExporSenha()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Name = "Teste", Email = "t@t.com", Password = "hash_secreto" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var lista = Assert.IsAssignableFrom<IEnumerable<UserDto>>(ok.Value);
        // UserDto não possui campo Password
        Assert.All(lista, u => Assert.Null(u.GetType().GetProperty("Password")));
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ComIdExistente_DeveRetornarOk()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User { Id = 1, Name = "Carlos", Email = "carlos@email.com", Password = "hash" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context);
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

        var controller = new UsersController(context);
        var dto = new RegisterDto { Name = "Outro", Email = "dup@email.com", Password = "senha123" };

        var result = await controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Create_DeveSalvarSenhaHasheada()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var controller = new UsersController(context);
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

        var controller = new UsersController(context);
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

        var controller = new UsersController(context);
        // Tenta atualizar User1 com o email de User2
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

        var controller = new UsersController(context);
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

        var controller = new UsersController(context);
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
