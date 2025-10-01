# WordRushBackend


REST API in ASP.NET Core with MVC architecture, data access using EF Core (PostgreSQL/Neon), Swagger documentation, and JWT authentication.
---

## 🏗️ Architecture and Decisions

### General Architecture (Client ⇒ Server)
Client (React Native) ⟶ Server (API .NET) ⟶ Database (PostgreSQL/Neon)
Communication via HTTPS and JWT. Enable CORS for the client domain.

### Internal Architecture (MVC + Layers)
- **Core**: Domain entities and interfaces.
- **Infrastructure**: `DbContext` (EF Core), repositories, migrations.
- **Web**: Controllers, DTOs, mappings, Swagger, Auth/CORS.

**Backend Decisions**
- **DB**: Neon (PostgreSQL, SSL required).
- **ORM**: EF Core + Npgsql.
- **Auth**: JWT Bearer.
- **Docs**: Swagger/Swashbuckle.
- **Migraciones**: `dotnet ef` from `Infrastructure` with *startup project* `Web`.

---

## ✅ Prerequisites
- .NET 7/8 SDK
- PostgreSQL (Neon) with a valid connection string
- Git, GitHub
- Git Hub Actions

---


# 📐 Development Standards

## Branching Strategy (Git)

**Modelo de ramas:**
- `main` → Only stable releases are merged here.
- `develop` → Integration branch where features are merged before release.
- `feature/` → Created from develop, follow the convention:

**Name conventions:**
- Format: `feature/<idTarea>`
- Example: `feature/1234-login-con-jwt`

## Merge Rules

- **To develop**:
  - Pull Request (PR) mandatory.
  - Strategy: **Squash & Merge**.

- **To main**:
  - Just allow from `develop`.

## Name convention

- **Namespaces / Types** (clases, structs, enums, records): `PascalCase`
- **Interfaces**: `I` + `PascalCase` → `IUserRepository`
- **Public methods / Properties / Events**: `PascalCase`
- **Parameters / Local variables**: `camelCase`
- **Private properties**: `_camelCase` (con guion bajo) → `_context`
- **Constants**: `UPPER_SNAKE_CASE`


## Directories y files

- **Solution**: `WordRush.sln`

### Projects
- `WordRush.Core`
- `WordRush.Infrastructure`
- `WordRush.Migrations`
- `WordRush.Repository`
- `WordRush.Web`
- `WordRush.Tests`

### Project Suffix Convention
- `.Web` → API
- `.Infrastructure`
- `.Core`
- `.Tests`

## API Routes

- **Base path**: `/api/v1/...`

### Conventions
- **Resources**: Use plural, lowercase, `kebab-case` for multiple words.
  - Examples:
    - `/api/v1/todos`
    - `/api/v1/user-profiles`

- **Identifiers**: Placed directly in the route
  - Example: `/api/v1/todos/{id}`

- **Sub-resources**:
  - Example: `/api/v1/users/{id}/reservations`

- **Not-CRUD Actions**: use a **verb** as a sub-route.
  - Examples:
    - `POST /api/v1/payments/{id}/capture`
    - `POST /api/v1/users/{id}/activate`

## Entities, DTOs y EF Core

### Entities 
- **Clases en singular**: `Todo`, `User`
- **Properties**: `PascalCase` → Ej.: `Id`, `Title`
- **Do not use unnecesary suffixes**: use `Todo` instead of `TodoEntity`

### DbContext
- **DbSet in plural**:

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

- The **pipelines** are executed automatically **when a `push` is made to any branch.**.
- The workflow includes the following stages:

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
# Restore and build
dotnet restore
dotnet build

# Run API
dotnet run --project src/Web
# Swagger: http(s)://localhost:<port>/swagger/index.html
```
