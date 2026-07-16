using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SG.Domain.Catalogos;
using SG.Domain.Catastro;
using ImportacionDomain = SG.Domain.Importacion;
using SG.Infrastructure.Auditoria;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia;

public class ApplicationDbContext
    : IdentityDbContext<UsuarioIdentidad, RolIdentidad, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditoriaEntidad> Auditorias => Set<AuditoriaEntidad>();

    // Catálogos
    public DbSet<UsoSuelo> UsosSuelo => Set<UsoSuelo>();
    public DbSet<Municipio> Municipios => Set<Municipio>();

    // Dominio catastral
    public DbSet<Propietario> Propietarios => Set<Propietario>();
    public DbSet<Predio> Predios => Set<Predio>();
    public DbSet<RelacionPredioPropietario> RelacionesPredioPropietario => Set<RelacionPredioPropietario>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();
    public DbSet<Construccion> Construcciones => Set<Construccion>();

    // Importación
    public DbSet<ImportacionDomain.PerfilImportacion> PerfilesImportacion => Set<ImportacionDomain.PerfilImportacion>();
    public DbSet<ImportacionDomain.MapeoColumna> MapeosColumna => Set<ImportacionDomain.MapeoColumna>();
    public DbSet<ImportacionDomain.EquivalenciaValor> EquivalenciasValor => Set<ImportacionDomain.EquivalenciaValor>();
    public DbSet<ImportacionDomain.Importacion> Importaciones => Set<ImportacionDomain.Importacion>();
    public DbSet<ImportacionDomain.DatasetVersion> DatasetVersiones => Set<ImportacionDomain.DatasetVersion>();
    public DbSet<ImportacionDomain.CapaParcela> CapasParcelas => Set<ImportacionDomain.CapaParcela>();
    public DbSet<ImportacionDomain.CapaEdificacion> CapasEdificaciones => Set<ImportacionDomain.CapaEdificacion>();
    public DbSet<ImportacionDomain.CapaPredioNoFotografiado> CapasPrediosNoFotografiados => Set<ImportacionDomain.CapaPredioNoFotografiado>();
    public DbSet<ImportacionDomain.CapaManzana> CapasManzanas => Set<ImportacionDomain.CapaManzana>();
    public DbSet<ImportacionDomain.CapaDistrito> CapasDistritos => Set<ImportacionDomain.CapaDistrito>();
    public DbSet<ImportacionDomain.CapaZona> CapasZonas => Set<ImportacionDomain.CapaZona>();
    public DbSet<ImportacionDomain.CapaVia> CapasVias => Set<ImportacionDomain.CapaVia>();
    public DbSet<ImportacionDomain.CapaAreaUrbana> CapasAreasUrbanas => Set<ImportacionDomain.CapaAreaUrbana>();
    public DbSet<ImportacionDomain.CapaPuntoGeodesico> CapasPuntosGeodesicos => Set<ImportacionDomain.CapaPuntoGeodesico>();
    public DbSet<ImportacionDomain.EsquemaCapaMunicipio> EsquemasCapas => Set<ImportacionDomain.EsquemaCapaMunicipio>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identidad");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
