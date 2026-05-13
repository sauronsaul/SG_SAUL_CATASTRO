# ADR 0012 — Protocolo para pruebas que tocan archivos de configuración del operador

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Durante el checkpoint 1.2, se necesitó verificar que `ApplicationDbContextFactory`
fallaba con un mensaje educativo cuando `.env` no estaba presente. La prueba
ejecutada fue:

```powershell
Remove-Item -Path ".env" -Force          # ← DESTRUCTIVO
dotnet ef dbcontext info ...             # test de fallo
# sin restauración garantizada
```

El test funcionó, pero el patrón fue riesgoso por dos razones:
1. `.env` puede contener contraseñas reales del operador. Si el `Remove-Item`
   tiene éxito y la sesión muere (crash, corte), el operador pierde su archivo.
2. Sin restauración explícita y verificada, el repositorio queda en estado
   inconsistente para la siguiente operación (`dotnet ef migrations add`, etc.).

En el caso concreto, `.env` no existía todavía en el repositorio, por lo que
el `Remove-Item` falló silenciosamente — el test pasó por un motivo diferente
al esperado. Eso es un "falso positivo estructural".

---

## Decisión

**Para cualquier test que toque archivos que el operador puede tener** (`.env`,
`appsettings.local.json`, certificados, claves privadas, etc.):

### Protocolo obligatorio

```powershell
# 1. BACKUP explícito antes de tocar
Rename-Item -Path "archivo.real" -NewName "archivo.real.bak" -ErrorAction Stop

# 2. Ejecutar el test
dotnet ef dbcontext info ...

# 3. RESTAURAR con verificación
Rename-Item -Path "archivo.real.bak" -NewName "archivo.real" -ErrorAction Stop

# 4. Confirmar que el archivo original está intacto
if (-not (Test-Path "archivo.real")) { throw "Restauración fallida — revisar manualmente" }
```

### Reglas

1. **`mv` / `Rename-Item` en lugar de `rm` / `Remove-Item`**. El backup preserva el
   contenido; el borrado no tiene vuelta atrás.
2. **`-ErrorAction Stop` en ambas operaciones** (backup Y restauración). Si alguna
   falla, el script se detiene antes de ejecutar el test o de dejar el sistema
   en estado inconsistente.
3. **Verificación explícita después de restaurar**. No asumir que funcionó.
4. **Ejecutar backup, test y restauración en un solo bloque atómico** (un solo
   comando compuesto, no pasos separados que el operador puede interrumpir).

### Alternativa preferida cuando sea posible

Si el test puede ejecutarse usando una ruta alternativa (directorio temporal,
variable de entorno vaciada) sin tocar el archivo real, esa es la opción preferida.
El archivo del operador nunca debería ser modificado.

---

## Consecuencias

**Positivas**:
- El operador nunca pierde datos de configuración por un test que salió mal.
- El estado del repositorio es predecible después de cada test.
- Los falsos positivos estructurales se detectan antes de que causen problemas.

**Negativas / compromisos**:
- El protocolo es más verboso que un simple `rm && test`.
- Requiere que el agente o el operador verifiquen el resultado de la restauración.

---

## Lección aprendida

En el caso que originó este ADR, el `Remove-Item` falló (`.env` no existía) pero
el test de "fallo educativo" también falló — no por la razón esperada sino porque
los binarios usados eran del build anterior (con fallback hardcoded). El test
arrojó un resultado que parecía correcto pero por motivos incorrectos. El protocolo
de backup/restauración habría evidenciado esto inmediatamente.
