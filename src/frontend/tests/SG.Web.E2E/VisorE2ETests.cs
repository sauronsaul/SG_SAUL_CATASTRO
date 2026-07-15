using Microsoft.Playwright;
using Xunit.Abstractions;

namespace SG.Web.E2E;

public sealed class VisorE2ETests(ITestOutputHelper output)
{
    [Fact]
    public async Task LoginCargaTileYEncuadraUyuni()
    {
        var baseUrl = ObtenerBaseUrl();
        var email = RequerirVariable("SG_E2E_EMAIL");
        // Lee SG_E2E_PASSWORD del entorno; sin literal. Falso positivo
        // de connection-string-password autorizado por el planificador.
        var password = RequerirVariable("SG_E2E_PASSWORD"); // gitleaks:allow
        var directorioArtefactos = ObtenerDirectorioArtefactos();
        var simularRegresionTiles = string.Equals(
            Environment.GetEnvironmentVariable("SG_E2E_SIMULAR_REGRESION_TILES"),
            "1",
            StringComparison.Ordinal);
        var timeoutTile = ObtenerTimeoutTile();
        Directory.CreateDirectory(directorioArtefactos);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = baseUrl.AbsoluteUri,
            ViewportSize = new() { Width = 1366, Height = 768 }
        });
        var page = await context.NewPageAsync();
        if (simularRegresionTiles)
        {
            await page.RouteAsync(
                "**/api/tiles/**",
                async route => await route.AbortAsync("failed"));
        }

        var tile200 = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        page.Response += (_, response) =>
        {
            if (response.Status == 200
                && Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.StartsWith("/api/tiles/", StringComparison.Ordinal))
            {
                tile200.TrySetResult(uri.AbsolutePath);
            }
        };

        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.GetByLabel("Correo electrónico", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Contraseña", new() { Exact = true }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Ingresar", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/visor", new() { Timeout = 30_000 });

        output.WriteLine($"regresion_tiles_simulada={simularRegresionTiles.ToString().ToLowerInvariant()}");
        var rutaTile = await tile200.Task.WaitAsync(timeoutTile);
        await page.Locator(".maplibregl-map").WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        var zoom = await page.EvaluateAsync<double>(
            """
            async () => {
                const contenedor = document.querySelector('.mapa');
                if (!contenedor?.id) return 0;
                const modulo = await import('/js/mapa.js');
                return modulo.obtenerCamara(contenedor.id)?.zoom ?? 0;
            }
            """);

        Assert.True(zoom > 10, $"El mapa quedó en zoom {zoom}; se esperaba zoom > 10.");

        var captura = Path.Combine(
            directorioArtefactos,
            $"visor-minimo-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
        await page.ScreenshotAsync(new()
        {
            Path = captura,
            FullPage = true
        });

        output.WriteLine($"base_url={baseUrl.GetLeftPart(UriPartial.Authority)}");
        output.WriteLine($"tile_200={rutaTile}");
        output.WriteLine($"zoom={zoom:F4}");
        output.WriteLine($"captura={Path.GetFullPath(captura)}");
    }

    private static Uri ObtenerBaseUrl()
    {
        var valor = Environment.GetEnvironmentVariable("SG_E2E_BASE_URL")
            ?? "http://localhost";
        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "SG_E2E_BASE_URL debe ser una URL absoluta HTTP o HTTPS.");
        }

        return uri;
    }

    private static string RequerirVariable(string nombre)
    {
        var valor = Environment.GetEnvironmentVariable(nombre);
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Falta la variable de entorno {nombre}.");

        return valor;
    }

    private static string ObtenerDirectorioArtefactos() =>
        Environment.GetEnvironmentVariable("SG_E2E_ARTIFACTS")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "e2e");

    private static TimeSpan ObtenerTimeoutTile()
    {
        var valor = Environment.GetEnvironmentVariable("SG_E2E_TILE_TIMEOUT_SECONDS");
        if (valor is null)
            return TimeSpan.FromSeconds(30);

        if (!int.TryParse(valor, out var segundos) || segundos is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "SG_E2E_TILE_TIMEOUT_SECONDS debe ser un entero entre 1 y 60.");
        }

        return TimeSpan.FromSeconds(segundos);
    }
}
