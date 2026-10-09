# Flujo: incident-response (incidente en producción)

## Objetivo
**Restablecer el servicio lo antes posible**, entender la causa raíz y evitar que se repita.

## Cuándo usarlo
La API está caída, degradada, devolviendo errores masivos o con un problema de seguridad activo.

## Roles
**observability-engineer** (diagnóstico) · **devops-engineer** (mitigación: rollback, escalado, configuración) · **security-reviewer** si es un incidente de seguridad. La sesión principal coordina y mantiene informado al usuario.

## Archivos a leer
`docs/runbooks/` (buscar el síntoma), `standards/10-observability.md`, `standards/13-deployment.md` y `docs/ai/AI_MEMORY.md`.

## Pasos
1. **Clasificar la severidad:**
   - **SEV1**: caída total o fuga de datos.
   - **SEV2**: función crítica degradada.
   - **SEV3**: problema menor con alternativa.
2. **Buscar un runbook** para el síntoma y seguirlo si existe.
3. **Diagnóstico rápido** (máximo unos minutos):
   - `/health/ready` y salud de las dependencias.
   - ¿Hubo un despliegue, una migración o un cambio de configuración reciente?
   - Errores principales en los logs (`log-analysis`, versión rápida).
4. **Mitigar primero** con la acción más segura y reversible:
   - Rollback a la versión anterior.
   - Escalar instancias.
   - Desactivar un feature flag.
   - Reiniciar.
   - Bloquear temporalmente un cliente abusivo.
   **Pedir confirmación al usuario** antes de cualquier acción en producción.
5. **Verificar** que el servicio se recuperó (health checks, tasa de errores, prueba manual).
6. **Investigar la causa raíz** con calma, ya mitigado (`log-analysis`).
7. **Corregir de fondo** con el flujo que corresponda (`new-feature`, `database-change`…), con tests que reproduzcan el problema.
8. **Postmortem** (sin culpables): línea de tiempo, impacto, causa raíz, qué funcionó, qué no y acciones con responsable.
9. **Aprender**: crear o mejorar el runbook, agregar la lección a `docs/ai/AI_MEMORY.md` y la alerta que faltó (`standards/10`).

## Incidente de seguridad (además)
- Contener: revocar sesiones (`RevokeAllAsync`), rotar secretos comprometidos (llave JWT, API keys, cadena de conexión) y bloquear el acceso.
- Preservar evidencia (logs) antes de limpiar.
- Evaluar datos expuestos y obligaciones de notificación según `docs/04` (normativa).

## Formato del postmortem
```
Incidente: <título> · SEV<n> · <fecha>
Línea de tiempo: detección → mitigación → resolución (con horas)
Impacto: <usuarios, peticiones, datos>
Causa raíz: <...>
Acciones: <acción · responsable · fecha>
```

## Reglas
- Mitigar antes de investigar. No hacer cambios en producción sin la confirmación del usuario.
- Comunicar el estado al usuario en cada paso importante.

## Definition of Done
- [ ] Servicio restablecido y verificado.
- [ ] Causa raíz identificada y corregida con test.
- [ ] Postmortem, runbook y `AI_MEMORY` actualizados.
