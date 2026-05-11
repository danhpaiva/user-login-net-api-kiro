using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using UserApiLogin.Models;
using UserApiLogin.Services;

namespace UserApiLogin.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
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

    [Fact]
    public void GenerateToken_DeveRetornarStringNaoVazia()
    {
        var user = new User { Id = 1, Name = "João", Email = "joao@email.com" };

        var token = _tokenService.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_DeveConterClaimsCorretas()
    {
        var user = new User { Id = 42, Name = "Maria", Email = "maria@email.com" };

        var token = _tokenService.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("42", jwt.Subject);
        Assert.Equal("maria@email.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Maria", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Name).Value);
    }

    [Fact]
    public void GenerateToken_DeveConterIssuerEAudienceCorretos()
    {
        var user = new User { Id = 1, Name = "Ana", Email = "ana@email.com" };

        var token = _tokenService.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
    }

    [Fact]
    public void GenerateToken_DeveExpirarNoTempoCorreto()
    {
        var user = new User { Id = 1, Name = "Carlos", Email = "carlos@email.com" };

        var antes = DateTime.UtcNow;
        var token = _tokenService.GenerateToken(user);
        var depois = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Expira em ~60 minutos
        Assert.True(jwt.ValidTo >= antes.AddMinutes(59));
        Assert.True(jwt.ValidTo <= depois.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_DeveConterJtiUnico()
    {
        var user = new User { Id = 1, Name = "Pedro", Email = "pedro@email.com" };

        var token1 = _tokenService.GenerateToken(user);
        var token2 = _tokenService.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert.NotEqual(jti1, jti2);
    }
}
