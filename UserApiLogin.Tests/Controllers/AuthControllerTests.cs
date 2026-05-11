using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UserApiLogin.Controllers;
using UserApiLogin.DTOs;
using UserApiLogin.Models;
using UserApiLogin.Services;
using UserApiLogin.Tests.Helpers;

namespace UserApiLogin.Tests.Controllers;

public class AuthControllerTests
{
    private readonly TokenService _tokenService;

    public AuthControllerTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"]        = "super-secret-key-for-unit-tests-32chars!!",
                ["JwtSettings:Issuer"]           = "TestIssuer",
                ["JwtSettings:Audience"]         = "TestAudience",
                ["JwtSettings:ExpiresInMinutes"] = "60"
            })
            .Build();

        _tokenService = new TokenService(config);
    }

    private AuthController CriarController(string dbName)
    {
        var context = DbContextHelper.CreateInMemoryContext(dbName);
        return new AuthController(context, _tokenService, NullLogger<AuthController>.Instance);
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveRetornarOkComToken()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var senha = "senha123";
        context.Users.Add(new User
        {
            Id = 1,
            Name = "João",
            Email = "joao@email.com",
            Password = BCrypt.Net.BCrypt.HashPassword(senha)
        });
        await context.SaveChangesAsync();

        var controller = new AuthController(context, _tokenService, NullLogger<AuthController>.Instance);
        var dto = new LoginDto { Email = "joao@email.com", Password = senha };

        var result = await controller.Login(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Login_ComEmailInexistente_DeveRetornarUnauthorized()
    {
        var controller = CriarController(nameof(Login_ComEmailInexistente_DeveRetornarUnauthorized));
        var dto = new LoginDto { Email = "naoexiste@email.com", Password = "qualquer" };

        var result = await controller.Login(dto);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ComSenhaErrada_DeveRetornarUnauthorized()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        context.Users.Add(new User
        {
            Id = 1,
            Name = "Ana",
            Email = "ana@email.com",
            Password = BCrypt.Net.BCrypt.HashPassword("senhaCorreta")
        });
        await context.SaveChangesAsync();

        var controller = new AuthController(context, _tokenService, NullLogger<AuthController>.Instance);
        var dto = new LoginDto { Email = "ana@email.com", Password = "senhaErrada" };

        var result = await controller.Login(dto);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveRetornarDadosDoUsuario()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var senha = "abc123";
        context.Users.Add(new User
        {
            Id = 1,
            Name = "Maria",
            Email = "maria@email.com",
            Password = BCrypt.Net.BCrypt.HashPassword(senha)
        });
        await context.SaveChangesAsync();

        var controller = new AuthController(context, _tokenService, NullLogger<AuthController>.Instance);
        var dto = new LoginDto { Email = "maria@email.com", Password = senha };

        var result = await controller.Login(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var tokenProp = value.GetType().GetProperty("token");
        var userProp  = value.GetType().GetProperty("user");

        Assert.NotNull(tokenProp);
        Assert.NotNull(userProp);

        var tokenValue = tokenProp!.GetValue(value) as string;
        Assert.False(string.IsNullOrWhiteSpace(tokenValue));

        var userValue = userProp!.GetValue(value) as UserDto;
        Assert.NotNull(userValue);
        Assert.Equal("maria@email.com", userValue!.Email);
        Assert.Equal("Maria", userValue.Name);
    }
}
