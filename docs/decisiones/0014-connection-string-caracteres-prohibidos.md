# ADR 0014 — Caracteres permitidos en passwords de connection strings

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Durante el Checkpoint 1.2, `dotnet ef database update` falló con `28P01` (auth
failure) a pesar de que el password en el contenedor PostgreSQL era correcto.

El diagnóstico reveló que el password contenía `;` (punto y coma). Npgsql
parsea la connection string usando `;` como separador de clave=valor. Cuando el
password contiene `;`, el parser lo trata como fin del valor `Password=...` e
intenta interpretar el resto como claves adicionales. El password que llega a
PostgreSQL es solo el fragmento anterior al primer `;`.

Ejemplo del fallo:
```
ConnectionStrings__Default=...;Password=Mi;Password123;Pooling=true
```
Npgsql lee: `Password=Mi` (incorrecto) y luego intenta parsear `Password123`
como clave sin valor — que descarta. PostgreSQL recibe `Mi`, falla autenticación.

El mismo problema ocurre con los caracteres: `=` `'` `"` espacio `\` entre otros.

## Decisión

**Todos los passwords en `.env` (POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD,
JWT_SECRET, etc.) DEBEN usar únicamente los siguientes caracteres:**

```
A-Z  a-z  0-9  -  _  .
```

**Caracteres PROHIBIDOS en passwords de este sistema:**

| Carácter | Razón |
|---|---|
| `;` | Separador de parámetros en Npgsql |
| `=` | Separador clave=valor en connection strings |
| `'` | Quoting en algunos parsers |
| `"` | Quoting en algunos parsers |
| `espacio` | Puede requerir quoting según el context |
| `\` | Escape character |
| `/` | Parte de URLs |
| `@` | Separador en URIs de conexión |
| `{` `}` `(` `)` `[` `]` `<` `>` | Shell y parsers varios |
| `!` `?` `&` `#` `$` `^` `*` `~` `` ` `` `\|` | Shell y expansión de variables |

## Cómo generar passwords compatibles

```powershell
# PowerShell (Windows):
-join ((65..90)+(97..122)+(48..57) | Get-Random -Count 32 | % {[char]$_})
```

```bash
# Linux / macOS:
LC_ALL=C tr -dc 'A-Za-z0-9_.-' </dev/urandom | head -c 32
```

Estos comandos generan 32 caracteres del conjunto permitido — suficiente entropía
para desarrollo y producción local.

## Alternativa técnica descartada (por ahora)

`NpgsqlConnectionStringBuilder` maneja correctamente el escaping de caracteres
especiales. Si en el futuro se necesitan passwords sin restricciones de caracteres,
la `ApplicationDbContextFactory` y `DependencyInjection.cs` deben construir la
connection string usando el builder en lugar de una string literal.

**Decisión actual**: no se implementa el builder porque agrega complejidad sin
beneficio real — las passwords generadas con el conjunto restringido son
criptográficamente seguras para los casos de uso del sistema.

## Consecuencias

**Positivas**:
- `dotnet ef database update` y `dotnet run` conectan correctamente vía TCP.
- No se requieren cambios en el código — es una regla operativa.
- `.env.example` documenta el conjunto de caracteres y el comando de generación.

**Negativas / compromisos**:
- Passwords generados externamente (gestores de contraseñas, etc.) pueden contener
  caracteres prohibidos. El operador debe verificar antes de pegar en `.env`.
- Si se usa `NpgsqlConnectionStringBuilder` en el futuro, esta restricción se vuelve
  innecesaria pero quedará como buena práctica por compatibilidad con otras capas
  (variables de shell, scripts bash, etc.).
