# Testing

> **Aplica a:** Todos los perfiles (casos de Auth solo con seguridad)  
> **Propósito:** Estrategia de pruebas, proyectos, paquetes, fixtures y casos mínimos.  
> Índice general: `standards/00-INDEX.md`

## Estrategia

| Tipo | Qué prueba | Base de datos | Proyecto |
|---|---|---|---|
| **Unit** | Validadores, `Result`, `PredicateBuilder`, mapeos, lógica pura de dominio | Ninguna | `tests/{Project}.UnitTests` |
| **Integration (services)** | Services con `UnitOfWork` real: reglas, duplicados, concurrencia, soft delete, transacciones | **El motor real del perfil** (Testcontainers: SQL Server o PostgreSQL) | `tests/{Project}.IntegrationTests` |
| **Integration (API)** | Endpoints completos: rutas, permisos, validación, códigos HTTP, ProblemDetails | El motor real del perfil (Testcontainers) | `tests/{Project}.IntegrationTests` |

> ⚠️ **No usar SQLite ni el proveedor InMemory para probar services.** El modelo usa índices filtrados, collations y tipos propios de cada motor, y esos proveedores no se comportan igual: los tests pasarían o fallarían por razones falsas.

## Proyecto de tests
La plantilla crea `tests/{Project}.IntegrationTests` (xUnit v3 + Microsoft.Testing.Platform, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers del motor, Respawn, Shouldly) con `ApiFactory`, `HealthTests` y, con seguridad, `TestUsers`. `kit-entity` agrega `{Entities}ApiTests.cs` por entidad.

Si hace falta un proyecto de unit tests (validadores, lógica pura):
```bash
dotnet new install xunit.v3.templates                                   # plantillas oficiales de xUnit v3 (una vez)
dotnet new xunit3 -n {Project}.UnitTests -o tests/{Project}.UnitTests
dotnet sln add tests/{Project}.UnitTests
dotnet add tests/{Project}.UnitTests reference src/{Project}.Api
```
Con `<OutputType>Exe</OutputType>`, sin `Version` en los `PackageReference` (gestión central) y sin `coverlet.collector`.

- Aserciones: **Shouldly** o las de xUnit. Evitar FluentAssertions 8+, que cambió a licencia comercial.
- Dobles de prueba para dependencias externas (email, HTTP): **NSubstitute**. No se "mockean" `IRepository` ni `IUnitOfWork`; se usa la BD real.
- Requiere **Docker** en la máquina y en CI para Testcontainers.
- `global.json` con `"test": { "runner": "Microsoft.Testing.Platform" }` y `tests/.editorconfig` con las reglas de tests (`standards/14`).

## Fixture de API — `tests/{Project}.IntegrationTests/ApiFactory.cs`
→ Código: [`templates/api/tests/KitApi.IntegrationTests/ApiFactory.cs`](../templates/api/tests/KitApi.IntegrationTests/ApiFactory.cs)
> Escrito para **xUnit v3** (`IAsyncLifetime` con `ValueTask`). Si la API de Testcontainers o Respawn cambia en una versión nueva, ajustar a la versión instalada.

## Helper de autenticación (con seguridad) — `tests/{Project}.IntegrationTests/TestUsers.cs`
→ Código: [`templates/api/tests/KitApi.IntegrationTests/TestUsers.cs`](../templates/api/tests/KitApi.IntegrationTests/TestUsers.cs)
Para probar permisos concretos, crear un usuario con un rol de prueba y loguearse con él.

## Tests por entidad — `tests/{Project}.IntegrationTests/{Entities}ApiTests.cs` (los genera `kit-entity`)
→ Código: [`templates/entity/tests/KitApi.IntegrationTests/KitEntitiesApiTests.cs`](../templates/entity/tests/KitApi.IntegrationTests/KitEntitiesApiTests.cs)

## Convenciones
- Nombre: `Metodo_Escenario_ResultadoEsperado` (`Create_WithDuplicateCode_Returns409WithCode`).
- Estructura Arrange / Act / Assert, separada por líneas en blanco.
- Un comportamiento por test. Sin lógica (`if`, bucles) dentro del test.
- Datos de prueba creados en el propio test o con helpers `Seed*Async`. Nunca depender del orden de ejecución.
- Fechas controladas con `FakeTimeProvider` cuando la lógica depende del tiempo.

## Casos mínimos por entidad de negocio
- [ ] Crear válido → 201 con `Location` y DTO.
- [ ] Crear inválido → 400 con `code = Validation.Failed` y `errors` por campo.
- [ ] Duplicado → 409 con su código.
- [ ] FK inexistente → 400.
- [ ] Obtener inexistente → 404.
- [ ] Listado paginado: `totalCount` correcto, orden estable y filtros.
- [ ] Actualizar con `RowVersion` vieja → 409.
- [ ] Eliminar → 204; después, `GET` → 404 (soft delete) y la fila sigue en BD con `IsDeleted = 1`.
- [ ] `[SEC]` Sin token → 401; sin permiso → 403; con permiso → OK.

## Casos mínimos de Auth `[SEC]`
- Login correcto e incorrecto, y lockout al 5.º intento.
- Email sin confirmar → 403.
- Refresh con rotación; reuso de un token rotado → 401 y sesión revocada.
- 2FA: setup, enable, login en dos pasos y login con código de recuperación.
- Endpoint con permiso: 403 sin el permiso, 200 con el permiso y 200 como Admin.
- Usuario desactivado o bloqueado: no puede hacer login ni refresh.

## Comandos
```bash
dotnet test
dotnet test --filter-class "*{Entities}ApiTests"
dotnet test --filter-method "*Returns401"
dotnet test --coverage --coverage-output-format cobertura
```
