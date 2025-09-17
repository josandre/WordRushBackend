# ✅ Definition of Done (DoD)

El objetivo de este documento es asegurar consistencia y calidad en cada entrega del proyecto.  
Una tarea / feature se considera **completada** cuando cumple con todos los siguientes criterios:

---

## 1. Código y Convenciones
- La rama fue creada desde `develop` siguiendo la convención:
  - `feature/<idTarea>-<slug>` (ejemplo: `feature/1234-login-con-jwt`).
- Se respetaron las **convenciones de nombres**:
  - Namespaces, clases, structs, enums, records → `PascalCase`
  - Interfaces → `I` + `PascalCase` (ej. `IUserRepository`)
  - Métodos públicos, propiedades, eventos → `PascalCase`
  - Variables locales y parámetros → `camelCase`
  - Campos privados → `_camelCase` (ej. `_context`)
  - Constantes → `UPPER_SNAKE_CASE`

---

## 2. Estructura de Carpetas y Proyectos
- Se mantienen los proyectos definidos en la solución:
  - `WordRush.Core`
  - `WordRush.Infrastructure`
  - `WordRush.Web`
  - `WordRush.Tests`
- Se usaron sufijos claros: `.Web`, `.Infrastructure`, `.Core`, `.Tests`.

---

## 3. Entidades, DTOs y EF Core
- Las entidades de dominio están en **singular** (`Todo`, `User`).
- Las propiedades usan `PascalCase` (ej.: `Id`, `Title`).
- No se usan sufijos innecesarios como `Entity`.
- `DbSet` definidos en plural dentro de `DbContext`.
  ```csharp
  public DbSet<Todo> Todos { get; set; }

## 4. Integración Continua

- El pipeline se ejecuta automáticamente tras el **push** en la rama.
- Todas las etapas deben completarse con éxito:
  1. Set up job
  2. Checkout code
  3. Setup .NET
  4. Restore dependencies
  5. Build
  6. Run tests
  7. Post Setup .NET
  8. Post Checkout code
  9. Complete job

---

## 6. Revisiones y Pull Requests

- El código pasó revisión por al menos **1 integrante del equipo**.
- El **merge a `develop`** se realizó vía Pull Request con **Squash & Merge**.
- El **merge a `main`** solo se hace desde `develop` después de validar la release.  
