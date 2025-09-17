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

## TEST

## ⚙️ Setup local

```bash
# Restaurar y compilar
dotnet restore
dotnet build

# Ejecutar API
dotnet run --project src/Web
# Swagger: http(s)://localhost:<puerto>/swagger/index.html

