# Flujo: test-generation (generar tests)

## Objetivo
Escribir tests automatizados que prueben los **criterios de aceptación y los errores** de un módulo, entidad o funcionalidad, siguiendo `standards/12`.

## Cuándo usarlo
- Al final de `new-entity`, `new-feature` o `api-endpoint`.
- El usuario pide "agregar tests" o se detecta código sin cobertura en `docs/03`.

## Roles
**qa-engineer**.

## Archivos a leer
`standards/12-testing.md`, `docs/modules/<módulo>.md` (criterios de aceptación y errores) y el código bajo prueba (service, controller, validadores).

## Pasos
1. **Verificar la infraestructura**: existen `tests/{Project}.UnitTests` y `tests/{Project}.IntegrationTests` con `ApiFactory` (`standards/12`). Si no, crearlos.
2. **Listar los casos**:
   - Uno o más por cada criterio de aceptación del módulo.
   - Uno por cada error de `{Entity}Errors`.
   - Los casos mínimos por entidad de `standards/12`.
   - `[SEC]`: sin token (401), sin permiso (403) y con permiso.
3. **Unit tests** para validadores y lógica pura (sin base de datos).
4. **Integration tests de API** con `ApiFactory`: datos creados en el propio test; BD limpia entre tests (`ResetDatabaseAsync`).
5. Verificar `status` **y** `code` del ProblemDetails; en 201, el header `Location`.
6. **Correr** `dotnet test`. Si un test falla por un bug real del código, **no ajustar el test para que pase**: reportarlo con el escenario y el resultado esperado.
7. **Trazabilidad**: nombres de test en la tabla de pruebas del módulo y en `docs/03`.

## Reglas
- Nombre: `Metodo_Escenario_ResultadoEsperado`. Arrange / Act / Assert.
- Sin SQLite ni InMemory para services; sin mocks de `IRepository`/`IUnitOfWork`.
- Tests independientes y deterministas (fechas con `FakeTimeProvider`).
- Sin `Thread.Sleep`; sin depender del orden de ejecución.

## Salida esperada
Tests en verde, o lista de fallas reales encontradas con su detalle.

## Definition of Done
- [ ] Todos los criterios de aceptación y errores tienen test.
- [ ] `dotnet test` en verde (o fallas reales reportadas).
- [ ] Trazabilidad actualizada en el módulo y en `docs/03`.
