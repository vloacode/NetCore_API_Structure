# Flujo: log-analysis (análisis de logs)

## Objetivo
Explicar **qué pasó y por qué** a partir de logs, trazas y métricas: errores, picos, comportamientos raros. También sirve como revisión diaria del sistema.

## Cuándo usarlo
- El usuario pega logs, un `traceId` o un error.
- Revisión diaria o semanal programada (por ejemplo, una tarea programada que corre este flujo cada mañana).
- Como paso de diagnóstico dentro de `incident-response`.

## Roles
**observability-engineer**.

## Archivos a leer
`standards/10-observability.md`, `docs/runbooks/` y `docs/04-non-functional-requirements.md` (umbrales).

## Pasos
1. **Delimitar**: ventana de tiempo, entorno, endpoints o usuarios afectados y `traceId` si hay.
2. **Obtener los datos**: logs del destino configurado (Application Insights, Seq, archivos), trazas por `traceId` y métricas (5xx, latencia, peticiones).
3. **Agrupar** errores por tipo, endpoint y código (`code` de ProblemDetails), con frecuencia y primera y última aparición.
4. **Correlacionar**: ¿coincide con un despliegue, una migración, un pico de tráfico o la caída de un tercero?
5. **Seguir una traza** representativa de punta a punta (request → service → SQL / HTTP externo).
6. **Concluir**: síntoma, causa probable (con evidencia), impacto (usuarios y peticiones afectadas) y acción recomendada.
7. Si el patrón es nuevo, crear o mejorar un runbook en `docs/runbooks/`.

## Revisión diaria (modo rutina)
Reportar en pocas líneas:
- Tasa de 5xx y los 3 errores más frecuentes con su tendencia.
- Endpoints más lentos (p95) frente al objetivo.
- Eventos de seguridad: logins fallidos, lockouts, reuso de refresh tokens, 403 inusuales `[SEC]`.
- Estado de health checks y de jobs.
- Acciones sugeridas (o "sin novedades").

## Formato del reporte
```
Resumen: <una línea>
Evidencia: <consultas, traceIds, líneas de log relevantes (sin datos sensibles)>
Causa probable: <...> (confirmada / hipótesis)
Impacto: <...>
Acción recomendada: <...>
```

## Reglas
- Citar evidencia para cada conclusión; distinguir **hecho** de **hipótesis**.
- No copiar datos sensibles de los logs al reporte (tokens, contraseñas, datos personales).

## Definition of Done
- [ ] Conclusión con evidencia y acción recomendada.
- [ ] Runbook creado o actualizado si el patrón es nuevo.
