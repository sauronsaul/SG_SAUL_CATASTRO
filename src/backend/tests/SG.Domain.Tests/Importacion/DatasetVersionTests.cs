using FluentAssertions;
using SG.Domain.Common;
using SG.Domain.Importacion;

namespace SG.Domain.Tests.Importacion;

public sealed class DatasetVersionTests
{
    private static DatasetVersion CrearEnCarga()
        => DatasetVersion.Crear(1, "051201", null, "Entrega de prueba");

    [Fact]
    public void Crear_DatosValidos_IniciaEnCarga()
    {
        var version = CrearEnCarga();

        version.Estado.Should().Be(EstadoDatasetVersion.EnCarga);
        version.NumeroVersion.Should().Be(1);
        version.MunicipioCodigo.Should().Be("051201");
    }

    [Fact]
    public void RegistrarProgreso_EnCarga_PersisteReportePreliminar()
    {
        var version = CrearEnCarga();

        version.RegistrarProgreso("{\"capaEnCurso\":\"capa_parcelas\"}");

        version.ReportePreliminar.Should().Contain("capa_parcelas");
    }

    [Fact]
    public void RegistrarErrorCarga_EnCarga_PersisteDetalle()
    {
        var version = CrearEnCarga();

        version.RegistrarErrorCarga("carga interrumpida por reinicio");

        version.ErrorCarga.Should().Be("carga interrumpida por reinicio");
    }

    [Fact]
    public void RegistrarReportePreview_AntesDePreviewListo_PersisteReporte()
    {
        var version = CrearEnCarga();

        version.RegistrarReportePreview("{\"validacion\":{\"bloqueantes\":[]}}");
        version.MarcarPreviewListo();

        version.ReportePreliminar.Should().Contain("bloqueantes");
        version.Estado.Should().Be(EstadoDatasetVersion.PreviewListo);
    }

    [Fact]
    public void Crear_NumeroNoPositivo_LanzaDomainException()
    {
        var act = () => DatasetVersion.Crear(0, "051201", null, "Entrega");

        act.Should().Throw<DomainException>().WithMessage("*mayor o igual a 1*");
    }

    [Theory]
    [InlineData("UYUNI")]
    [InlineData("05120")]
    [InlineData("05120A")]
    public void Crear_CodigoIneInvalido_LanzaDomainException(string codigo)
    {
        var act = () => DatasetVersion.Crear(1, codigo, null, "Entrega");

        act.Should().Throw<DomainException>().WithMessage("*seis digitos ASCII*");
    }

    [Fact]
    public void MarcarPreviewListo_DesdeEnCarga_Transiciona()
    {
        var version = CrearEnCarga();

        version.MarcarPreviewListo();

        version.Estado.Should().Be(EstadoDatasetVersion.PreviewListo);
    }

    [Fact]
    public void MarcarPreviewListo_DesdeEstadoInvalido_LanzaDomainException()
    {
        var version = CrearEnCarga();
        version.MarcarFallida();

        var act = () => version.MarcarPreviewListo();

        act.Should().Throw<DomainException>().WithMessage("*Fallida*PreviewListo*");
    }

    [Fact]
    public void Activar_DesdePreviewListo_TransicionaYRegistraTrazabilidad()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();
        var usuario = Guid.NewGuid();

        version.Activar(usuario);

        version.Estado.Should().Be(EstadoDatasetVersion.Activa);
        version.ActivadoPor.Should().Be(usuario);
        version.ActivadoAt.Should().NotBeNull();
    }

    [Fact]
    public void Activar_DesdeEnCarga_LanzaDomainException()
    {
        var version = CrearEnCarga();

        var act = () => version.Activar(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*EnCarga*Activa*PreviewListo*");
    }

    [Fact]
    public void Archivar_DesdeActiva_TransicionaYRegistraTrazabilidad()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();
        version.Activar(Guid.NewGuid());
        var usuario = Guid.NewGuid();

        version.Archivar(usuario);

        version.Estado.Should().Be(EstadoDatasetVersion.Archivada);
        version.ArchivadoPor.Should().Be(usuario);
        version.ArchivadoAt.Should().NotBeNull();
    }

    [Fact]
    public void ReactivarDesdeArchivada_TransicionaYLimpiaMarcaArchivada()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();
        version.Activar(Guid.NewGuid());
        version.Archivar(Guid.NewGuid());
        var usuario = Guid.NewGuid();

        version.ReactivarDesdeArchivada(usuario);

        version.Estado.Should().Be(EstadoDatasetVersion.Activa);
        version.ActivadoPor.Should().Be(usuario);
        version.ArchivadoAt.Should().BeNull();
        version.ArchivadoPor.Should().BeNull();
    }

    [Fact]
    public void RegistrarResumenReconciliacion_Activa_PersisteJson()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();
        version.Activar(Guid.NewGuid());

        version.RegistrarResumenReconciliacion("{\"altas\":1}");

        version.ResumenReconciliacion.Should().Contain("altas");
    }

    [Fact]
    public void Archivar_DesdePreviewListo_LanzaDomainException()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();

        var act = () => version.Archivar(Guid.NewGuid());

        act.Should().Throw<DomainException>().WithMessage("*PreviewListo*Archivada*Activa*");
    }

    [Fact]
    public void MarcarFallida_DesdeEnCarga_Transiciona()
    {
        var version = CrearEnCarga();

        version.MarcarFallida();

        version.Estado.Should().Be(EstadoDatasetVersion.Fallida);
    }

    [Fact]
    public void MarcarFallida_DesdePreviewListo_LanzaDomainException()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();

        var act = () => version.MarcarFallida();

        act.Should().Throw<DomainException>().WithMessage("*PreviewListo*Fallida*EnCarga*");
    }

    [Fact]
    public void Descartar_DesdePreviewListo_Transiciona()
    {
        var version = CrearEnCarga();
        version.MarcarPreviewListo();

        version.Descartar();

        version.Estado.Should().Be(EstadoDatasetVersion.Descartada);
    }

    [Fact]
    public void Descartar_DesdeEnCarga_LanzaDomainException()
    {
        var version = CrearEnCarga();

        var act = () => version.Descartar();

        act.Should().Throw<DomainException>().WithMessage("*EnCarga*Descartada*PreviewListo*");
    }
}
