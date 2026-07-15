using Microsoft.Playwright;
using Xunit.Abstractions;

namespace SG.Web.E2E;

public sealed class VisorE2ETests(ITestOutputHelper output)
{
    private const string ResaltadoTriplete111 =
        "{\"relleno\":[\"all\",[\"==\",[\"get\",\"cod_uv\"],1],[\"==\",[\"get\",\"cod_man\"],1],[\"==\",[\"get\",\"cod_pred\"],1]]," +
        "\"linea\":[\"all\",[\"==\",[\"get\",\"cod_uv\"],1],[\"==\",[\"get\",\"cod_man\"],1],[\"==\",[\"get\",\"cod_pred\"],1]]}";
    private const string ResaltadoVacio =
        "{\"relleno\":[\"==\",[\"get\",\"cod_uv\"],-1],\"linea\":[\"==\",[\"get\",\"cod_uv\"],-1]}";

    [Fact]
    public async Task LoginCargaTileBuscaPredioYMuestraFicha()
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
        var ficha200 = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        page.Response += (_, response) =>
        {
            if (response.Status == 200
                && Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.StartsWith("/api/tiles/", StringComparison.Ordinal))
            {
                tile200.TrySetResult(uri.AbsolutePath);
            }

            if (response.Status == 200
                && Uri.TryCreate(response.Url, UriKind.Absolute, out var fichaUri)
                && fichaUri.AbsolutePath.Equals(
                    "/api/predios/buscar",
                    StringComparison.Ordinal))
            {
                ficha200.TrySetResult(fichaUri.PathAndQuery);
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
        var zoomInicial = await ObtenerZoomAsync(page);
        Assert.True(
            zoomInicial > 10,
            $"El mapa quedó en zoom {zoomInicial}; se esperaba zoom > 10.");
        Assert.Equal(0, await page.GetByText("_errorPredio", new() { Exact = true }).CountAsync());

        await page.GetByLabel("Distrito", new() { Exact = true }).FillAsync("1");
        await page.GetByLabel("Manzana", new() { Exact = true }).FillAsync("1");
        await page.GetByLabel("Predio", new() { Exact = true }).FillAsync("1");
        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "Buscar predio", Exact = true }).ClickAsync();

        var rutaFicha = await ficha200.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var panel = page.GetByLabel("Ficha del predio", new() { Exact = true });
        await panel.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await page.WaitForFunctionAsync(
            """
            async () => {
                const contenedor = document.querySelector('.mapa');
                if (!contenedor?.id) return false;
                const modulo = await import('/js/mapa.js');
                return (modulo.obtenerCamara(contenedor.id)?.zoom ?? 0) > 17;
            }
            """,
            null,
            new() { Timeout = 30_000 });
        var zoomPredio = await ObtenerZoomAsync(page);
        var resaltadoBusqueda = await ObtenerResaltadoAsync(page);
        Assert.Equal(ResaltadoTriplete111, resaltadoBusqueda);

        await CambiarZoomAsync(page, ".maplibregl-ctrl-zoom-out", 5, "< 15");
        var resaltadoZoomOut = await ObtenerResaltadoAsync(page);
        Assert.Equal(ResaltadoTriplete111, resaltadoZoomOut);
        await CambiarZoomAsync(page, ".maplibregl-ctrl-zoom-in", 5, "> 17");
        var resaltadoZoomIn = await ObtenerResaltadoAsync(page);
        Assert.Equal(ResaltadoTriplete111, resaltadoZoomIn);

        await Assertions.Expect(panel).ToContainTextAsync("1 / 1 / 1");
        await Assertions.Expect(panel).ToContainTextAsync("Fila de origen11883");
        await Assertions.Expect(panel).ToContainTextAsync("Código geográfico04-12-05-01");
        await Assertions.Expect(panel).ToContainTextAsync("EstadoImportado");
        await Assertions.Expect(panel).ToContainTextAsync("Declarada238,3470 m²");
        await Assertions.Expect(panel).ToContainTextAsync("Gráfica238,3466 m²");
        await Assertions.Expect(panel).ToContainTextAsync("Tipo de inmuebleVIV");
        await Assertions.Expect(panel).ToContainTextAsync("VíaCOLON Y SUCRE");
        await Assertions.Expect(panel).ToContainTextAsync("Dataset UYUNI — versión interna 3");

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "Cerrar ficha", Exact = true }).ClickAsync();
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.Equal(ResaltadoVacio, await ObtenerResaltadoAsync(page));
        var lienzo = page.Locator(".maplibregl-canvas");
        var cajaLienzo = await lienzo.BoundingBoxAsync();
        Assert.NotNull(cajaLienzo);
        var respuestaClic = await page.RunAndWaitForResponseAsync(
            async () => await lienzo.ClickAsync(new()
            {
                Position = new()
                {
                    X = cajaLienzo!.Width / 2,
                    Y = cajaLienzo.Height / 2
                }
            }),
            response => response.Status == 200
                && Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.Equals("/api/predios/buscar", StringComparison.Ordinal),
            new() { Timeout = 30_000 });
        await panel.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });
        await Assertions.Expect(panel).ToContainTextAsync("1 / 1 / 1");
        var resaltadoClic = await ObtenerResaltadoAsync(page);
        Assert.Equal(ResaltadoTriplete111, resaltadoClic);

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
        output.WriteLine($"ficha_200={rutaFicha}");
        output.WriteLine($"ficha_click_200={new Uri(respuestaClic.Url).PathAndQuery}");
        output.WriteLine($"zoom_inicial={zoomInicial:F4}");
        output.WriteLine($"zoom_predio={zoomPredio:F4}");
        output.WriteLine($"resaltado_busqueda={resaltadoBusqueda}");
        output.WriteLine($"resaltado_zoom_out={resaltadoZoomOut}");
        output.WriteLine($"resaltado_zoom_in={resaltadoZoomIn}");
        output.WriteLine($"resaltado_clic={resaltadoClic}");
        output.WriteLine("triplete=1/1/1 fila=11883 declarada=238.3470 grafica=238.3466 version=3");
        output.WriteLine($"captura={Path.GetFullPath(captura)}");
    }

    private static Task<double> ObtenerZoomAsync(IPage page) =>
        page.EvaluateAsync<double>(
            """
            async () => {
                const contenedor = document.querySelector('.mapa');
                if (!contenedor?.id) return 0;
                const modulo = await import('/js/mapa.js');
                return modulo.obtenerCamara(contenedor.id)?.zoom ?? 0;
            }
            """);

    private static Task<string> ObtenerResaltadoAsync(IPage page) =>
        page.EvaluateAsync<string>(
            """
            async () => {
                const contenedor = document.querySelector('.mapa');
                if (!contenedor?.id) return "";
                const modulo = await import('/js/mapa.js');
                return JSON.stringify(modulo.obtenerResaltado(contenedor.id));
            }
            """);

    private static async Task CambiarZoomAsync(
        IPage page,
        string selector,
        int cantidad,
        string comparacion)
    {
        var control = page.Locator(selector);
        Assert.Equal(1, await control.CountAsync());
        for (var indice = 0; indice < cantidad; indice++)
        {
            await control.ClickAsync(new() { Force = true });
            await page.WaitForTimeoutAsync(350);
        }

        await page.WaitForFunctionAsync(
            $$"""
            async () => {
                const contenedor = document.querySelector('.mapa');
                if (!contenedor?.id) return false;
                const modulo = await import('/js/mapa.js');
                return (modulo.obtenerCamara(contenedor.id)?.zoom ?? 0) {{comparacion}};
            }
            """,
            null,
            new() { Timeout = 30_000 });
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
