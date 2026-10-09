# Testing

> **Aplica a:** Todos los perfiles  
> **Propósito:** Estrategia y casos mínimos de pruebas.  
> Índice general: `standards/00-INDEX.md`

## Testing

- **Unit tests de services** (xUnit): `AppDbContext` sobre **SQLite in-memory** (`Microsoft.EntityFrameworkCore.Sqlite`, conexión `DataSource=:memory:` abierta durante el test) + `UnitOfWork` real + `ICurrentUserService` y `TimeProvider` falsos (`Microsoft.Extensions.TimeProvider.Testing` → `FakeTimeProvider`).
  - SQLite no soporta `rowversion`. En tests, configurar `RowVersion` como `IsConcurrencyToken()` con un valor asignado por el test, o usar Testcontainers con SQL Server.
- **Integration tests**: `WebApplicationFactory<Program>`.
  - Reemplazar la cadena de conexión por una BD de test (Testcontainers SQL Server recomendado).
  - Sobrescribir `Jwt:SigningKey` y `Seed:*` por configuración.
  - Obtener un token real llamando a `/api/auth/login` con el admin del seed, o generarlo con `ITokenService` resuelto del contenedor.
- **Casos mínimos de Auth**:
  - Login correcto e incorrecto, y lockout al 5.º intento.
  - Email sin confirmar → 403.
  - Refresh con rotación; reuso de token → 401 y sesión revocada.
  - 2FA: setup, enable, login en dos pasos y login con código de recuperación.
  - Endpoint con permiso: 403 sin el permiso, 200 con el permiso y 200 como Admin.
