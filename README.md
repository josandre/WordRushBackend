# WordRushBackend


API REST en **ASP.NET Core** con arquitectura **MVC**, acceso a datos con **EF Core (PostgreSQL/Neon)**, documentación **Swagger** y autenticación **JWT**.

---

## 🏗️ Arquitectura y decisiones

### Arquitectura general (Cliente ⇒ Servidor)
**Cliente (React Native)** ⟶ **Servidor (API .NET)** ⟶ **Base de Datos (PostgreSQL/Neon)**  
Comunicación vía **HTTPS** y **JWT**. Habilitar **CORS** para el dominio del cliente.

### Arquitectura interna (MVC + Capas)
- **Core**: Entidades de dominio e interfaces.
- **Infrastructure**: `DbContext` (EF Core), repositorios, migraciones.
- **Web**: Controllers, DTOs, mapeos, Swagger, Auth/CORS.

**Decisiones BE**
- **DB**: Neon (PostgreSQL, SSL requerido).
- **ORM**: EF Core + Npgsql.
- **Auth**: JWT Bearer.
- **Docs**: Swagger/Swashbuckle.
- **Migraciones**: `dotnet ef` desde `Infrastructure` con *startup project* `Web`.

---

## ✅ Requisitos previos
- .NET 7/8 SDK
- PostgreSQL (Neon) con cadena de conexión válida
- Git, GitHub
- Git Hub Actions

---


# 📐 Estándares de Desarrollo

## Estrategia de ramas (Git)

**Modelo de ramas:**
- `main` → Solo versiones **estables**.
- `develop` → Rama de integración de **features**.
- `feature/` → Ramas de trabajo creadas desde `develop`.

**Convenciones de nombres:**
- Formato: `feature/<idTarea>`
- Ejemplo: `feature/1234-login-con-jwt`

## Reglas de Merge

- **Hacia develop**:
  - Pull Request (PR) obligatorio.
  - Estrategia: **Squash & Merge**.

- **Hacia main**:
  - Solo permitido desde `develop`.

## Convención de nombres

- **Namespaces / Tipos** (clases, structs, enums, records): `PascalCase`
- **Interfaces**: `I` + `PascalCase` → `IUserRepository`
- **Métodos públicos / Propiedades / Eventos**: `PascalCase`
- **Parámetros / Variables locales**: `camelCase`
- **Campos privados**: `_camelCase` (con guion bajo) → `_context`
- **Constantes**: `UPPER_SNAKE_CASE`


## Carpetas y archivos

- **Solución**: `WordRush.sln`

### Proyectos
- `WordRush.Core`
- `WordRush.Infrastructure`
- `WordRush.Migrations`
- `WordRush.Repository`
- `WordRush.Web`
- `WordRush.Tests`

### Convención de sufijos de proyectos
- `.Web` → API
- `.Infrastructure`
- `.Core`
- `.Tests`

## Rutas API

- **Base path versionado**: `/api/v1/...`

### Convenciones
- **Recursos**: plural, minúsculas, `kebab-case` si hay varias palabras.
  - Ejemplos:
    - `/api/v1/todos`
    - `/api/v1/user-profiles`

- **Identificadores**: en la ruta
  - Ejemplo: `/api/v1/todos/{id}`

- **Sub-recursos**:
  - Ejemplo: `/api/v1/users/{id}/reservations`

- **Acciones no-CRUD**: usar **verbo** como subruta
  - Ejemplos:
    - `POST /api/v1/payments/{id}/capture`
    - `POST /api/v1/users/{id}/activate`

## Entidades, DTOs y EF Core

### Entidades (Dominio)
- **Clases en singular**: `Todo`, `User`
- **Propiedades**: `PascalCase` → Ej.: `Id`, `Title`
- **Evitar sufijos innecesarios**: usar `Todo` en lugar de `TodoEntity`

### DbContext
- **DbSet en plural**:

```csharp
public DbSet<Todo> Todos { get; set; }
```

## 🗄️ Running Migrations

### Setup
Entity Framework migrations rely on the `DOTNET_ENVIRONMENT` variable.  
By default, `dotnet run` assumes **Production**, so user secrets will not be loaded unless we force the environment to **Development**.

#### macOS / Linux (bash or zsh)

1. Open your shell profile (`~/.zshrc` or `~/.bashrc` depending on your shell).
2. Add the following line:

   ```bash
   export DOTNET_ENVIRONMENT=Development
3. Reload your shell config `source ~/.zshrc` or `source ~/.bashrc` 

#### Powershell
1. Execute in your powershell `setx DOTNET_ENVIRONMENT "Development"`

After this, from the repository root go to on `src/Web`, and execute `dotnet user-secrets set "ConnectionStrings:WordRushDb" "<connection-string>"`

### Adding Migrations
1. From the repository root move to `src/Migrations`
2. Execute `dotnet ef migrations add <MigrationName>`

### Running Migrations

1. From the repository root move to `src/Migrations`
2. Execute `dotnet run`

---
## ⚙️ Pipelines CI/CD

- Los **pipelines** se ejecutan automáticamente **cuando se realiza un `push` en cualquier rama**.
- El flujo incluye las siguientes etapas:

1. **Set up job**
2. **Checkout code**
3. **Setup .NET**
4. **Restore dependencies**
5. **Build**
6. **Run tests**
7. **Post Setup .NET**
8. **Post Checkout code**
9. **Complete job**
---

## ⚙️ Setup local

```bash
# Restaurar y compilar
dotnet restore
dotnet build

# Ejecutar API
dotnet run --project src/Web
# Swagger: http(s)://localhost:<puerto>/swagger/index.html
```
