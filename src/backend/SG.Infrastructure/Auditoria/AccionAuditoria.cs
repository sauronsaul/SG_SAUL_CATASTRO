namespace SG.Infrastructure.Auditoria;

public enum AccionAuditoria
{
    Insert,
    Update,
    Delete,
    Login,
    LoginFallido,
    Logout,
    RefreshToken,
    ReutilizacionDetectada,
    Export,
    CambioPassword
}
