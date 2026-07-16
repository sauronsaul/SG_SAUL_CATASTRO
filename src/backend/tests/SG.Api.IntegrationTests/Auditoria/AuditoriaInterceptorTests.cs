using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Infrastructure.Auditoria;
using SG.Infrastructure.Persistencia;

namespace SG.Api.IntegrationTests.Auditoria;

[Collection("Postgres")]
public sealed class AuditoriaInterceptorTests : IDisposable
{
    private readonly SgApiFactory _factory;

    public AuditoriaInterceptorTests(PostgreSqlFixture fixture)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    // ── Helper: insertar un Predio y devolver el AuditoriaEntidad generado ─
    private static async Task<AuditoriaEntidad> CrearPredioYObtenerAuditAsync(
        ApplicationDbContext db, string zonaSufijo)
    {
        var ubicacion = UbicacionCatastral.Crear("901", zonaSufijo[1..], "001").Value;
        var predio = Predio.CrearImportado("051201", ubicacion, 100m, Guid.NewGuid()).Value;
        db.Predios.Add(predio);
        await db.SaveChangesAsync();

        var auditId = predio.Id.ToString();
        var registro = await db.Set<AuditoriaEntidad>()
            .AsNoTracking()
            .FirstAsync(a => a.EntidadTipo == nameof(Predio) && a.EntidadId == auditId);

        return registro;
    }

    // ── Test 1: Guard C# — bloquea Modified ───────────────────────────────
    [Fact]
    public async Task GuardCsharp_AuditoriaModificada_LanzaInvalidOperationException()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Paso 1: generar un registro de auditoría real
        var registro = await CrearPredioYObtenerAuditAsync(db, "G1");

        // Paso 2: adjuntar al ChangeTracker en estado Unchanged
        var entidadTrackeada = await db.Set<AuditoriaEntidad>().FindAsync(registro.Id);
        entidadTrackeada.Should().NotBeNull();

        // Paso 3: mutar → estado Modified
        entidadTrackeada!.Resultado = "TAMPERED";

        // Paso 4: SaveChangesAsync debe lanzar el guard
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Violación del invariante append-only*");
    }

    // ── Test 2: Guard C# — bloquea Deleted ────────────────────────────────
    [Fact]
    public async Task GuardCsharp_AuditoriaEliminada_LanzaInvalidOperationException()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Paso 1: generar un registro de auditoría real
        var registro = await CrearPredioYObtenerAuditAsync(db, "G2");

        // Paso 2: adjuntar al ChangeTracker en estado Unchanged
        var entidadTrackeada = await db.Set<AuditoriaEntidad>().FindAsync(registro.Id);
        entidadTrackeada.Should().NotBeNull();

        // Paso 3: marcar para borrar → estado Deleted
        db.Set<AuditoriaEntidad>().Remove(entidadTrackeada!);

        // Paso 4: SaveChangesAsync debe lanzar el guard
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Violación del invariante append-only*");
    }

    // ── Test 3: Trigger DB — bloquea UPDATE directo (Capa 2) ──────────────
    [Fact]
    public async Task TriggerDB_UpdateDirecto_LanzaExcepcionNpgsql()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Crear un registro de auditoría real
        var registro = await CrearPredioYObtenerAuditAsync(db, "T3");

        // UPDATE directo que bypasea EF Core y Capa 1
        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE auditoria.auditoria SET resultado='TAMPERED' WHERE id={0}",
            registro.Id);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*Violación append-only*");
    }

    // ── Test 4: Trigger DB — bloquea TRUNCATE (Capa 2, statement-level) ───
    [Fact]
    public async Task TriggerDB_Truncate_LanzaExcepcionNpgsql()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Crear al menos un registro para que el TRUNCATE no sea vacío
        await CrearPredioYObtenerAuditAsync(db, "T4");

        // TRUNCATE debe ser bloqueado por el trigger statement-level
        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE auditoria.auditoria;");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*Violación append-only*");
    }

    // ── Test 5: Existencia del trigger en information_schema ──────────────
    [Fact]
    public async Task Trigger_ExisteEnInformationSchema()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var count = await db.Database
            .SqlQueryRaw<int>(@"
SELECT COUNT(*)::int AS ""Value""
FROM information_schema.triggers
WHERE trigger_schema      = 'auditoria'
  AND event_object_table  = 'auditoria'
  AND trigger_name        = 'trg_auditoria_immutable'")
            .SingleAsync();

        count.Should().BeGreaterThanOrEqualTo(1,
            because: "M009 debe haber creado trg_auditoria_immutable sobre auditoria.auditoria");
    }
}
