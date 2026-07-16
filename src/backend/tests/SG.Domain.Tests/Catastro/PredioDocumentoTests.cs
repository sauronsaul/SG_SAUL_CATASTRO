using FluentAssertions;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioDocumentoTests
{
    private const string MunicipioCodigo = "051201";
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static Predio PredioNuevo()
    {
        var ubicacion = UbicacionCatastral.Crear("001", "0001", "0001").Value;
        return Predio.Crear(MunicipioCodigo, ubicacion, 250m, Guid.NewGuid(), UsuarioId).Value;
    }

    // ── Predio.AgregarDocumento → cubre Documento.Crear ────────────────────

    [Fact]
    public void AgregarDocumento_DatosValidos_AgregaALaColeccion()
    {
        var predio = PredioNuevo();

        var result = predio.AgregarDocumento(
            "cedula.pdf", "application/pdf", 4096,
            "predios/123/cedula.pdf", TipoDocumento.CI, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Documentos.Should().HaveCount(1);
        predio.Documentos.Single().NombreArchivo.Should().Be("cedula.pdf");
        predio.Documentos.Single().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void AgregarDocumento_MultiplesDocumentos_AgregaTodos()
    {
        var predio = PredioNuevo();

        predio.AgregarDocumento("doc1.pdf", "application/pdf", 1024,
            "predios/1/doc1.pdf", TipoDocumento.CI, UsuarioId);
        predio.AgregarDocumento("doc2.pdf", "application/pdf", 2048,
            "predios/1/doc2.pdf", TipoDocumento.EscrituraPublica, UsuarioId);

        predio.Documentos.Should().HaveCount(2);
    }

    // ── Predio.EliminarDocumento → cubre Documento.Eliminar ────────────────

    [Fact]
    public void EliminarDocumento_DocumentoExistente_ConMotivo_EsExito_IsDeletedTrue()
    {
        var predio = PredioNuevo();
        var docResult = predio.AgregarDocumento(
            "cedula.pdf", "application/pdf", 4096,
            "predios/1/cedula.pdf", TipoDocumento.CI, UsuarioId);
        var docId = docResult.Value.Id;

        var result = predio.EliminarDocumento(docId, UsuarioId, "Documento subido por error.");

        result.IsSuccess.Should().BeTrue();
        predio.Documentos.Single().IsDeleted.Should().BeTrue();
        predio.Documentos.Single().MotivoEliminacion.Should().Be("Documento subido por error.");
    }

    [Fact]
    public void EliminarDocumento_DocumentoInexistente_EsFailure_NoEncontrado()
    {
        var predio = PredioNuevo();
        var idInexistente = Guid.NewGuid();

        var result = predio.EliminarDocumento(idInexistente, UsuarioId, "Motivo válido.");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DocumentoErrores.NoEncontrado);
    }

    [Fact]
    public void EliminarDocumento_SinMotivo_EsFailure_MotivoRequerido()
    {
        var predio = PredioNuevo();
        var docResult = predio.AgregarDocumento(
            "cedula.pdf", "application/pdf", 4096,
            "predios/1/cedula.pdf", TipoDocumento.CI, UsuarioId);
        var docId = docResult.Value.Id;

        var result = predio.EliminarDocumento(docId, UsuarioId, "");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DocumentoErrores.MotivoRequerido);
    }

    [Fact]
    public void EliminarDocumento_MotivoSoloEspacios_EsFailure_MotivoRequerido()
    {
        var predio = PredioNuevo();
        var docResult = predio.AgregarDocumento(
            "cedula.pdf", "application/pdf", 4096,
            "predios/1/cedula.pdf", TipoDocumento.CI, UsuarioId);
        var docId = docResult.Value.Id;

        var result = predio.EliminarDocumento(docId, UsuarioId, "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DocumentoErrores.MotivoRequerido);
    }

    [Fact]
    public void EliminarDocumento_DocumentoYaEliminado_EsFailure_YaEliminado()
    {
        var predio = PredioNuevo();
        var docResult = predio.AgregarDocumento(
            "cedula.pdf", "application/pdf", 4096,
            "predios/1/cedula.pdf", TipoDocumento.CI, UsuarioId);
        var docId = docResult.Value.Id;
        predio.EliminarDocumento(docId, UsuarioId, "Primera eliminación.");

        var result = predio.EliminarDocumento(docId, UsuarioId, "Segunda eliminación.");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DocumentoErrores.YaEliminado);
    }
}
