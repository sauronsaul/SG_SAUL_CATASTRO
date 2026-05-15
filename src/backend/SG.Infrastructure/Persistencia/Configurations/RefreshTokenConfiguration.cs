using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia.Configurations;

/// <summary>
/// NOTA: No se define query filter espejo sobre el filtro de soft-delete de UsuarioIdentidad.
/// La revocación de refresh tokens al eliminar un usuario debe ser explícita en el handler.
/// Ver ADR 0025.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", schema: "identidad");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.ReplacedByToken)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedByIp)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(x => x.RevokedByIp)
            .HasMaxLength(45);

        builder.Property(x => x.RevokedReason)
            .HasMaxLength(50);

        builder.Property(x => x.ExpiresAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.RevokedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.Token).IsUnique();

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.IsActive);
    }
}
