using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Contracts.Autenticacion;
using SG.Contracts.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Infrastructure.Identidad;
using SG.Infrastructure.Persistencia;

namespace SG.Api.IntegrationTests.Catastro;

[Collection("Postgres")]
public sealed class PredioE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;

    public PredioE2ETests(PostgreSqlFixture fixture)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
        _clientAdmin = CrearClienteAutenticado(LoginAdminAsync().GetAwaiter().GetResult());
    }

    public void Dispose()
    {
        _clientAdmin.Dispose();
        _factory.Dispose();
    }

    // ── T12.1: Flujo completo P1-P10 ────────────────────────────────────────

    [Fact]
    public async Task FlujCompleto_CrearPredioYValidar_FlujoP1P10EnVerde()
    {
        var usoSueloId = await ObtenerUsoSueloIdAsync();

        // P1: Crear propietario persona natural → 201
        var propietarioId = await CrearPersonaNaturalAsync("Carlos", "Mamani Quispe", Guid.NewGuid().ToString("N")[..12]);
        propietarioId.Should().NotBeEmpty();

        // P2: Crear predio en Borrador → 201
        var predioId = await CrearPredioAsync("001", "0001", "0001", 300m, usoSueloId);
        predioId.Should().NotBeEmpty();

        // P3: Vincular propietario → 204
        var vincular = await _clientAdmin.PutAsJsonAsync(
            $"/api/predios/{predioId}/propietario",
            new { PropietarioId = propietarioId, TipoDerecho = TipoDerecho.Propietario, Porcentaje = 100m, VigenteDesde = DateOnly.FromDateTime(DateTime.Today) });
        vincular.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // P4: Subir documento → 201
        var docResp = await SubirDocumentoAsync(predioId);
        docResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // P5: Enviar a revisión → 204
        var enviar = await _clientAdmin.PostAsync($"/api/predios/{predioId}/estado/enviar-revision", null);
        enviar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // P6: Validar → 204
        var validar = await _clientAdmin.PostAsync($"/api/predios/{predioId}/estado/validar", null);
        validar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // P7: GET predio → Validado + código catastral presente
        var getResp = await _clientAdmin.GetAsync($"/api/predios/{predioId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var predio = await getResp.Content.ReadFromJsonAsync<PredioDto>(JsonOpts);
        predio!.Estado.Should().Be("Validado");
        predio.CodigoCatastral.Should().NotBeNullOrWhiteSpace();
        // Formato esperado: 02-006-028-001-0001-0001
        predio.CodigoCatastral.Should().MatchRegex(@"^\d{2}-\d{3}-\d{3}-\d{3}-\d{4}-\d{4}$");
    }

    // ── T12.2: Autorización asimétrica ─────────────────────────────────────

    [Fact]
    public async Task Historial_AdminVe200_TecnicoVe403()
    {
        var usoSueloId = await ObtenerUsoSueloIdAsync();
        var predioId = await CrearPredioAsync("004", "0004", "0004", 150m, usoSueloId);
        await _clientAdmin.PostAsync($"/api/predios/{predioId}/estado/enviar-revision", null);

        // Admin ve el historial
        var respAdmin = await _clientAdmin.GetAsync($"/api/predios/{predioId}/historial");
        respAdmin.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tecnico recibe 403
        var tokenTecnico = await CrearTecnicoYLoguearAsync();
        using var clienteTecnico = CrearClienteAutenticado(tokenTecnico);
        var respTecnico = await clienteTecnico.GetAsync($"/api/predios/{predioId}/historial");
        respTecnico.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CualquierEndpoint_SinToken_Retorna401()
    {
        using var clienteSinToken = _factory.CreateClient();

        var resp = await clienteSinToken.GetAsync("/api/predios");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── T12.3: Regresión Bug 2 ──────────────────────────────────────────────

    [Fact]
    public async Task VincularPropietario_PersisteLaRelacion_RegresionBug2()
    {
        // Este test cazaría el DbUpdateConcurrencyException del Bug 2 si reapareciera.
        // Causa: EF Core emitía UPDATE en vez de INSERT por falta de ValueGeneratedNever()
        // en las PKs de entidades hijas (RelacionPredioPropietario, Documento, HistorialEstado).

        var usoSueloId = await ObtenerUsoSueloIdAsync();
        var propietarioId = await CrearPersonaNaturalAsync("Pedro", "Flores Cruz", Guid.NewGuid().ToString("N")[..12]);
        var predioId = await CrearPredioAsync("002", "0002", "0002", 200m, usoSueloId);

        var resp = await _clientAdmin.PutAsJsonAsync(
            $"/api/predios/{predioId}/propietario",
            new { PropietarioId = propietarioId, TipoDerecho = TipoDerecho.Propietario, Porcentaje = 100m, VigenteDesde = DateOnly.FromDateTime(DateTime.Today) });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar explícitamente que la fila fue INSERTADA en la BD
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var relaciones = await db.RelacionesPredioPropietario
            .Where(r => r.PredioId == predioId)
            .ToListAsync();

        relaciones.Should().HaveCount(1, "la relación debe haberse insertado, no actualizado");
        relaciones[0].PropietarioId.Should().Be(propietarioId);
        relaciones[0].EsVigente.Should().BeTrue();
    }

    // ── T12.4: Unicidad ─────────────────────────────────────────────────────

    [Fact]
    public async Task CedulaDuplicada_SegundaCreacion_Retorna409()
    {
        var cedulaUnica = Guid.NewGuid().ToString("N")[..12];
        await CrearPersonaNaturalAsync("Ana", "Lopez Torres", cedulaUnica);

        var resp = await _clientAdmin.PostAsJsonAsync(
            "/api/propietarios/persona-natural",
            new { Nombre = "Otra", Apellidos = "Lopez", Cedula = cedulaUnica, Email = (string?)null, Telefono = (string?)null, Direccion = (string?)null });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TripleteCatastralDuplicado_SegundoRegistro_Retorna409()
    {
        var usoSueloId = await ObtenerUsoSueloIdAsync();

        // Predio 1: crear, enviar, validar → ocupa "02-006-028-003-0003-0003"
        var p1 = await CrearPredioAsync("003", "0003", "0003", 100m, usoSueloId);
        await _clientAdmin.PostAsync($"/api/predios/{p1}/estado/enviar-revision", null);
        var v1 = await _clientAdmin.PostAsync($"/api/predios/{p1}/estado/validar", null);
        v1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Predio 2: misma zona/manzana/lote → mismo código catastral esperado
        var v2 = await _clientAdmin.PostAsJsonAsync(
            "/api/predios",
            new
            {
                UbicacionZona = "003",
                UbicacionManzana = "0003",
                UbicacionLote = "0003",
                UbicacionBarrio = (string?)null,
                UbicacionDireccion = (string?)null,
                UbicacionReferencia = (string?)null,
                SuperficieDeclarada = 120m,
                UsoSueloId = usoSueloId,
            });

        v2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> ObtenerUsoSueloIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uso = await db.UsosSuelo.FirstAsync();
        return uso.Id;
    }

    private async Task<Guid> CrearPersonaNaturalAsync(string nombre, string apellidos, string cedula)
    {
        var resp = await _clientAdmin.PostAsJsonAsync(
            "/api/propietarios/persona-natural",
            new { Nombre = nombre, Apellidos = apellidos, Cedula = cedula, Email = (string?)null, Telefono = (string?)null, Direccion = (string?)null });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CrearPredioAsync(string zona, string manzana, string lote, decimal superficie, Guid usoSueloId)
    {
        var resp = await _clientAdmin.PostAsJsonAsync(
            "/api/predios",
            new
            {
                UbicacionZona = zona,
                UbicacionManzana = manzana,
                UbicacionLote = lote,
                UbicacionBarrio = (string?)null,
                UbicacionDireccion = (string?)null,
                UbicacionReferencia = (string?)null,
                SuperficieDeclarada = superficie,
                UsoSueloId = usoSueloId
            });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SubirDocumentoAsync(Guid predioId)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 37, 80, 68, 70 }; // %PDF magic bytes
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "archivo", "cedula.pdf");
        content.Add(new StringContent(((int)TipoDocumento.CI).ToString(System.Globalization.CultureInfo.InvariantCulture)), "tipoDocumento");
        return await _clientAdmin.PostAsync($"/api/predios/{predioId}/documentos", content);
    }

    private async Task<string> CrearTecnicoYLoguearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioIdentidad>>();
        var email = $"tecnico_{Guid.NewGuid():N}@test.local";
        const string password = "TecnicoTest!Password123";
        var tecnico = new UsuarioIdentidad
        {
            UserName = email,
            Email = email,
            NombreCompleto = "Técnico Test",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };
        var createResult = await userManager.CreateAsync(tecnico, password);
        createResult.Succeeded.Should().BeTrue("no se pudo crear el usuario Tecnico para el test");
        await userManager.AddToRoleAsync(tecnico, "Tecnico");

        var resp = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, password));
        resp.EnsureSuccessStatusCode();
        var loginBody = await resp.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts);
        return loginBody!.AccessToken;
    }

    private async Task<string> LoginAdminAsync()
    {
        using var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(PostgreSqlFixture.AdminEmail, PostgreSqlFixture.AdminPassword));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts);
        return body!.AccessToken;
    }

    private HttpClient CrearClienteAutenticado(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
