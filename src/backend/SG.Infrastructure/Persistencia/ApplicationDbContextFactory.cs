using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SG.Infrastructure.Persistencia;

public sealed class ApplicationDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        CargarDotEnv();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                """
                No se encontró la connection string para los EF Core design-time tools.

                Asegúrate de:
                  1. Tener un archivo .env en la raíz del repositorio
                     (C:\Proyectos\SG_SAUL_CATASTRO\.env o equivalente).
                  2. Que .env contenga la variable:
                       ConnectionStrings__Default=Host=...;Port=...;Database=...;Username=...;Password=...
                  3. Ejecutar los comandos dotnet ef desde src/backend/ o desde
                     cualquier directorio donde la búsqueda hacia arriba encuentre el .env.

                El archivo .env.example en la raíz del repositorio muestra el
                formato exacto requerido.
                """);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "identidad");
        });
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    // SG.Infrastructure no depende de DotNetEnv (es una preocupación de la capa Api).
    // La factory implementa su propio cargador mínimo para tiempo de diseño:
    // lee el .env línea por línea buscando hacia arriba en el árbol de directorios.
    private static void CargarDotEnv()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
            {
                foreach (var linea in File.ReadAllLines(candidate))
                {
                    if (string.IsNullOrWhiteSpace(linea) || linea.TrimStart().StartsWith('#'))
                        continue;

                    var separador = linea.IndexOf('=');
                    if (separador <= 0) continue;

                    var clave = linea[..separador].Trim();
                    var valor = linea[(separador + 1)..].Trim();

                    // Solo establece si no existe ya en el proceso (no sobreescribe OS-level vars).
                    if (Environment.GetEnvironmentVariable(clave) is null)
                        Environment.SetEnvironmentVariable(clave, valor);
                }
                return;
            }

            dir = Path.GetDirectoryName(dir)!;
        }
    }
}
