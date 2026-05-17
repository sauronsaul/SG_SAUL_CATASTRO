using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Contracts.Autenticacion;

namespace SG.Api.IntegrationTests.Auth;

[Collection("Postgres")]
public sealed class AuthE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly SgApiFactory _factory;
    private readonly HttpClient _client;

    public AuthE2ETests(PostgreSqlFixture fixture)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Login_AdminCredencialesCorrectas_Retorna200ConTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(PostgreSqlFixture.AdminEmail, PostgreSqlFixture.AdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts);
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Usuario.Email.Should().Be(PostgreSqlFixture.AdminEmail);
        result.Usuario.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Login_PasswordIncorrecto_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(PostgreSqlFixture.AdminEmail, "WrongPassword!999"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmailNoRegistrado_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("noexiste@test.com", "AnyPassword!123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ConTokenValido_Retorna200ConDatosUsuario()
    {
        var token = await LoginYObtenerAccessTokenAsync();
        using var clienteAuth = CrearClienteConBearer(token);

        var response = await clienteAuth.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UsuarioDto>(JsonOpts);
        result!.Email.Should().Be(PostgreSqlFixture.AdminEmail);
        result.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Refresh_TokenValido_RetornaTokensNuevos()
    {
        var loginResult = await LoginAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(loginResult.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(JsonOpts);
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(loginResult.RefreshToken);
    }

    [Fact]
    public async Task Logout_TokenActivo_Retorna204()
    {
        var loginResult = await LoginAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(loginResult.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<LoginResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(PostgreSqlFixture.AdminEmail, PostgreSqlFixture.AdminPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts))!;
    }

    private async Task<string> LoginYObtenerAccessTokenAsync()
        => (await LoginAsync()).AccessToken;

    private HttpClient CrearClienteConBearer(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
