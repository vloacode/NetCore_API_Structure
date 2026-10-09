# Flujo: security-review (revisión de seguridad)

## Objetivo
Detectar vulnerabilidades y desvíos del baseline de seguridad en un cambio, un módulo o la API completa. **Solo lectura.**

## Cuándo usarlo
- Cambios en autenticación, permisos, endpoints públicos, datos personales, archivos o integraciones.
- Antes de salir a producción y de forma periódica.

## Roles
**security-reviewer**.

## Archivos a leer
`docs/00-MASTER_CONTEXT.md` (perfil), `standards/09-security-baseline.md`; `[SEC]` `standards/05-authentication-identity.md`, `standards/06-authorization-permissions.md`, `docs/07-permissions-matrix.md`; el código bajo revisión.

## Pasos
1. **Alcance**: listar los endpoints y componentes a revisar (controllers, services, configuración, `Program.cs`).
2. **Autorización por función** (API5): cada acción tiene `[HasPermission]` con el permiso correcto según `docs/07` `[SEC]`. Sin seguridad: cada escritura es pública por decisión documentada o está protegida con API key.
3. **Autorización por objeto** (API1): si el recurso tiene dueño, el filtro está en la spec. Probar mentalmente cambiar el id por uno ajeno.
4. **Propiedades** (API3): requests sin campos controlados por el servidor; DTOs sin datos internos o sensibles.
5. **Consumo de recursos** (API4): paginación, `MaxPageSize`, límites de tamaño, rate limiting.
6. **Autenticación** (API2) `[SEC]`: lockout, 2FA, rotación de refresh tokens y respuestas genéricas sin revelar si existen usuarios.
7. **Configuración** (API8): HTTPS/HSTS, headers, CORS con orígenes exactos, OpenAPI no expuesto en producción, ProblemDetails sin detalles internos.
8. **Secretos y datos sensibles**: nada en el código, en `appsettings*.json` ni en logs.
9. **SSRF y terceros** (API7, API10): URLs externas en lista blanca; respuestas de terceros validadas; `HttpClient` con timeout.
10. **Dependencias**: `dotnet list package --vulnerable --include-transitive`.
11. **Reporte**.

## Formato del reporte
Por severidad (Crítica / Alta / Media / Baja). Cada hallazgo: `archivo:línea`, escenario de ataque concreto, impacto, corrección sugerida y referencia (OWASP API Top 10 o standard). Cerrar con un resumen: total por severidad y si bloquea el despliegue (cualquier Crítica o Alta bloquea).

## Reglas
- No modificar archivos.
- Desvíos del baseline solo con un ADR aceptado; si no lo hay, es un hallazgo.

## Definition of Done
- [ ] Checklist por endpoint de `standards/09` aplicado a todo el alcance.
- [ ] Dependencias revisadas.
- [ ] Reporte entregado con veredicto de despliegue.
