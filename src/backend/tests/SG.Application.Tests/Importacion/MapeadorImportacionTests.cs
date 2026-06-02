using FluentAssertions;
using NetTopologySuite.Geometries;
using SG.Application.Abstractions;
using SG.Application.Importacion;
using SG.Domain.Importacion;

namespace SG.Application.Tests.Importacion;

public sealed class MapeadorImportacionTests
{
    private readonly MapeadorImportacion _sut = new();

    // Geometría mínima válida para tests que no prueban ausencia de geometría.
    private static readonly Geometry GeometriaTest =
        new GeometryFactory().CreatePoint(new Coordinate(500_000, 8_000_000));

    // Polígono vacío (IsEmpty == true, no nulo) para el test de geometría degenerada.
    private static readonly Geometry GeometriaVaciaTest =
        new GeometryFactory().CreatePolygon();

    // ── helpers ────────────────────────────────────────────────────────────

    private static PerfilImportacion PerfilPrediosSimple()
    {
        var p = PerfilImportacion.Crear("test-predios", TipoCapa.Predios, "test.shp");
        p.AgregarMapeo("cod_uv",     "UbicacionCatastral.Zona",      esObligatorio: true);
        p.AgregarMapeo("cod_man",    "UbicacionCatastral.Manzana",   esObligatorio: true);
        p.AgregarMapeo("cod_pred",   "UbicacionCatastral.Lote",      esObligatorio: true);
        p.AgregarMapeo("superficie", "SuperficieDeclarada",          esObligatorio: true);
        p.AgregarMapeo("nompro",     "PropietarioReferencia",        esObligatorio: false);
        return p;
    }

    // Por defecto incluye geometría válida. Los tests de ausencia de geometría
    // pasan conGeometria: false — no se usa Geometry? porque null ?? GeometriaTest
    // nunca puede ser null con el operador ??.
    // geometriaVacia: true entrega un polígono IsEmpty==true (no nulo).
    private static RegistroCrudoShapefile RegistroValido(
        Dictionary<string, object?>? atributos = null,
        bool proyeccionDesconocida = false,
        bool conGeometria = true,
        bool geometriaVacia = false) =>
        new(
            Geometria: !conGeometria ? null : geometriaVacia ? GeometriaVaciaTest : GeometriaTest,
            Atributos: atributos ?? new Dictionary<string, object?>
            {
                ["cod_uv"]     = 4L,
                ["cod_man"]    = 12L,
                ["cod_pred"]   = 7L,
                ["superficie"] = 250.50,
                ["nompro"]     = "Juan Perez",
            },
            ProyeccionDesconocida: proyeccionDesconocida,
            SridOrigenWkt: null);

    // ── clasificación ──────────────────────────────────────────────────────

    [Fact]
    public void Mapear_CamposObligatoriosPresentes_DevuelveOk()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 1);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Ok);
        resultado.Errores.Should().BeEmpty();
        resultado.Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Mapear_CampoTripleta_Ausente_DevuelveRechazada()
    {
        // cod_uv → destino UbicacionCatastral.Zona (tripleta) → rechazo
        var attrs = new Dictionary<string, object?>
        {
            // "cod_uv" ausente
            ["cod_man"]    = 12L,
            ["cod_pred"]   = 7L,
            ["superficie"] = 250.0,
        };

        var resultado = _sut.Mapear(RegistroValido(attrs), PerfilPrediosSimple(), numeroFila: 1);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("UbicacionCatastral.Zona"));
    }

    [Fact]
    public void Mapear_CampoTripleta_Nulo_DevuelveRechazada()
    {
        var attrs = new Dictionary<string, object?>
        {
            ["cod_uv"]     = null,   // presente pero nulo
            ["cod_man"]    = 12L,
            ["cod_pred"]   = 7L,
            ["superficie"] = 250.0,
        };

        var resultado = _sut.Mapear(RegistroValido(attrs), PerfilPrediosSimple(), numeroFila: 2);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("UbicacionCatastral.Zona"));
    }

    [Fact]
    public void Mapear_CampoObligatorioNoTripleta_Ausente_DevuelveAdvertencia()
    {
        // 'superficie' es obligatorio pero NO es parte de la tripleta →
        // la fila debe entrar marcada para revisión, no descartarse.
        var attrs = new Dictionary<string, object?>
        {
            ["cod_uv"]   = 4L,
            ["cod_man"]  = 12L,
            ["cod_pred"] = 7L,
            // "superficie" ausente — obligatorio no-tripleta
        };

        var resultado = _sut.Mapear(RegistroValido(attrs), PerfilPrediosSimple(), numeroFila: 3);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Advertencia);
        resultado.Errores.Should().BeEmpty();
        resultado.Advertencias.Should().ContainSingle(a => a.Contains("superficie"));
    }

    [Fact]
    public void Mapear_CampoOpcionalAusente_DevuelveAdvertencia()
    {
        var attrs = new Dictionary<string, object?>
        {
            ["cod_uv"]     = 4L,
            ["cod_man"]    = 12L,
            ["cod_pred"]   = 7L,
            ["superficie"] = 250.0,
            // "nompro" ausente (opcional)
        };

        var resultado = _sut.Mapear(RegistroValido(attrs), PerfilPrediosSimple(), numeroFila: 4);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Advertencia);
        resultado.Errores.Should().BeEmpty();
        resultado.Advertencias.Should().ContainSingle(a => a.Contains("nompro"));
    }

    [Fact]
    public void Mapear_GeometriaNula_DevuelveRechazadaConErrorGeometria()
    {
        // Sin geometría la fila es irrecuperable, aunque todos los atributos estén presentes.
        var resultado = _sut.Mapear(
            RegistroValido(conGeometria: false),
            PerfilPrediosSimple(),
            numeroFila: 5);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("geometría"));
    }

    [Fact]
    public void Mapear_GeometriaVacia_DevuelveRechazadaConErrorGeometria()
    {
        // Un polígono IsEmpty==true no es nulo pero tampoco es válido para el SIG.
        var resultado = _sut.Mapear(
            RegistroValido(geometriaVacia: true),
            PerfilPrediosSimple(),
            numeroFila: 7);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("geometría"));
    }

    [Fact]
    public void Mapear_SinGeometria_TodosAtributosCompletos_AunAsıRechazada()
    {
        // La regla es absoluta: sin geometría = rechazo, sin importar los atributos.
        var attrs = new Dictionary<string, object?>
        {
            ["cod_uv"]     = 4L,
            ["cod_man"]    = 12L,
            ["cod_pred"]   = 7L,
            ["superficie"] = 250.0,
            ["nompro"]     = "Juan Perez",
        };

        var resultado = _sut.Mapear(
            new RegistroCrudoShapefile(null, attrs, false, null),
            PerfilPrediosSimple(),
            numeroFila: 6);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("geometría"));
    }

    [Fact]
    public void Mapear_ProyeccionDesconocida_DevuelveAdvertencia()
    {
        var resultado = _sut.Mapear(
            RegistroValido(proyeccionDesconocida: true),
            PerfilPrediosSimple(),
            numeroFila: 1);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Advertencia);
        resultado.Advertencias.Should().ContainSingle(a => a.Contains("Proyección desconocida"));
    }

    [Fact]
    public void Mapear_ProyeccionDesconocidaYTripleta_Ausente_DevuelveRechazada()
    {
        var attrs = new Dictionary<string, object?>
        {
            // "cod_uv" (tripleta) ausente
            ["cod_man"]    = 12L,
            ["cod_pred"]   = 7L,
            ["superficie"] = 250.0,
        };

        var resultado = _sut.Mapear(
            RegistroValido(attrs, proyeccionDesconocida: true),
            PerfilPrediosSimple(),
            numeroFila: 1);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().ContainSingle(e => e.Contains("UbicacionCatastral.Zona"));
        resultado.Advertencias.Should().ContainSingle(a => a.Contains("Proyección desconocida"));
    }

    // ── conversión de tipos ────────────────────────────────────────────────

    [Fact]
    public void Mapear_ValorLong_SeConvierteAStringInvariant()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 1);

        resultado.ValoresMapeados["UbicacionCatastral.Zona"].Should().Be("4");
        resultado.ValoresMapeados["UbicacionCatastral.Manzana"].Should().Be("12");
        resultado.ValoresMapeados["UbicacionCatastral.Lote"].Should().Be("7");
    }

    [Fact]
    public void Mapear_ValorDouble_SeConvierteAStringInvariant()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 1);

        resultado.ValoresMapeados["SuperficieDeclarada"].Should().Be("250.5");
    }

    [Fact]
    public void Mapear_ValorString_SePreservaSinConversion()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 1);

        resultado.ValoresMapeados["PropietarioReferencia"].Should().Be("Juan Perez");
    }

    // ── equivalencias ──────────────────────────────────────────────────────

    [Fact]
    public void Mapear_ConEquivalencia_AplicaValorDestino()
    {
        var perfil = PerfilImportacion.Crear("test-eq", TipoCapa.Predios, "test.shp");
        var mapeo = perfil.AgregarMapeo("tip_inm", "TipoInmuebleOrigen", esObligatorio: false);
        perfil.AgregarEquivalencia(mapeo.Id, "C", "Casa");
        perfil.AgregarEquivalencia(mapeo.Id, "T", "Terreno");

        var attrs = new Dictionary<string, object?> { ["tip_inm"] = "C" };
        var registro = new RegistroCrudoShapefile(GeometriaTest, attrs, false, null);

        var resultado = _sut.Mapear(registro, perfil, numeroFila: 1);

        resultado.ValoresMapeados["TipoInmuebleOrigen"].Should().Be("Casa");
        resultado.Clasificacion.Should().Be(ClasificacionFila.Ok);
    }

    [Fact]
    public void Mapear_SinEquivalenciaCoincidente_PreservaValorOriginal()
    {
        var perfil = PerfilImportacion.Crear("test-eq", TipoCapa.Predios, "test.shp");
        var mapeo = perfil.AgregarMapeo("tip_inm", "TipoInmuebleOrigen", esObligatorio: false);
        perfil.AgregarEquivalencia(mapeo.Id, "C", "Casa");

        var attrs = new Dictionary<string, object?> { ["tip_inm"] = "X" };
        var registro = new RegistroCrudoShapefile(GeometriaTest, attrs, false, null);

        var resultado = _sut.Mapear(registro, perfil, numeroFila: 1);

        resultado.ValoresMapeados["TipoInmuebleOrigen"].Should().Be("X");
    }

    // ── metadata ──────────────────────────────────────────────────────────

    [Fact]
    public void Mapear_NumeroFilaSePreservaEnResultado()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 42);

        resultado.NumeroFila.Should().Be(42);
    }

    [Fact]
    public void Mapear_GeometriaValidaSeTransparentaEnResultado()
    {
        var resultado = _sut.Mapear(RegistroValido(), PerfilPrediosSimple(), numeroFila: 1);

        resultado.Geometria.Should().BeSameAs(GeometriaTest);
    }

    [Fact]
    public void Mapear_TripletaCompletaFaltante_TresErroresDeRechazo()
    {
        // Los 3 campos de la tripleta ausentes → 3 errores de rechazo.
        // Campos no-tripleta (obligatorios u opcionales) ausentes → advertencias, no errores.
        var attrs = new Dictionary<string, object?>();

        var resultado = _sut.Mapear(RegistroValido(attrs), PerfilPrediosSimple(), numeroFila: 1);

        resultado.Clasificacion.Should().Be(ClasificacionFila.Rechazada);
        resultado.Errores.Should().HaveCount(3);
        // La regla: solo los campos de la tripleta deben estar en los errores de rechazo.
        resultado.Errores.Should().Contain(e => e.Contains("UbicacionCatastral.Zona"));
        resultado.Errores.Should().Contain(e => e.Contains("UbicacionCatastral.Manzana"));
        resultado.Errores.Should().Contain(e => e.Contains("UbicacionCatastral.Lote"));
        // superficie y nompro NO deben aparecer en errores (van a advertencias).
        resultado.Errores.Should().NotContain(e => e.Contains("superficie"));
        resultado.Errores.Should().NotContain(e => e.Contains("nompro"));
    }
}
