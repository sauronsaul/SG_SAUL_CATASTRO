using FluentAssertions;
using SG.Api.Startup;

namespace SG.Api.IntegrationTests;

public sealed class MigrationStartupGuardTests
{
    [Fact]
    public void DebeAplicarMigraciones_VariableAusente_RetornaFalse()
    {
        var shouldApply = MigrationStartupGuard.DebeAplicarMigraciones(null);

        shouldApply.Should().BeFalse();
    }
}
