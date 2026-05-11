# ADR 0005 — Central Package Management con Directory.Packages.props

**Fecha**: 2026-05-11
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

La solución SG tiene 8 proyectos (.NET): 5 de producción y 3 de pruebas.
Sin gestión centralizada de versiones, cada `.csproj` especifica su propia
versión de cada paquete NuGet. En una solución multiproyecto esto genera:

- Versiones inconsistentes del mismo paquete entre proyectos (ej. xunit 2.9.2
  en un test y 2.9.3 en otro).
- Actualizaciones costosas: hay que editar N archivos para subir una versión.
- Riesgo de conflictos en el grafo de dependencias transitivas.

## Decisión

Se usa **Central Package Management (CPM)** de MSBuild mediante un único
archivo `src/backend/Directory.Packages.props` con:

```xml
<PropertyGroup>
  <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
</PropertyGroup>
```

Cada `<PackageVersion>` declara la versión canónica del paquete. Los `.csproj`
individuales solo declaran `<PackageReference Include="Nombre" />` sin versión.

## Flujo operativo

```bash
# Agregar un paquete nuevo (el SDK actualiza ambos archivos automáticamente)
dotnet add SG.Application/SG.Application.csproj package MediatR

# Actualizar la versión de un paquete (solo editar Directory.Packages.props)
# <PackageVersion Include="MediatR" Version="15.0.0" />
```

## Consecuencias

**Positivas**:
- Única fuente de verdad para versiones: `Directory.Packages.props`.
- Actualización de un paquete = editar una sola línea.
- Imposible tener versiones inconsistentes del mismo paquete entre proyectos.
- Compatible con `dotnet add package` (el SDK detecta CPM y actualiza el props).

**Negativas / compromisos**:
- Requiere `tests/Directory.Build.props` con `<Import>` explícito al padre
  para que los proyectos de tests hereden correctamente (MSBuild se detiene
  en el primer `Directory.Build.props` encontrado).
- Colaboradores nuevos deben conocer el patrón CPM antes de agregar paquetes.

## Referencia

- [Central Package Management — docs.microsoft.com](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
