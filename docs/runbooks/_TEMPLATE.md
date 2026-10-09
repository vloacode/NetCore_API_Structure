# Runbook: {{Síntoma o procedimiento}}

<!-- IA: copiar como docs/runbooks/<tema-en-kebab-case>.md. Un runbook por síntoma ("la API responde 503")
o procedimiento ("restaurar backup", "rotar la llave JWT"). Pasos ejecutables por alguien con poco contexto, a las 3 a.m.
Lo usan incident-response y log-analysis; se crea o mejora después de cada incidente. -->

| Campo | Valor |
|---|---|
| Severidad típica | {{SEV1 caída total / SEV2 degradado / SEV3 menor}} |
| Última revisión | {{AAAA-MM-DD}} |
| Dueño | {{Persona o equipo}} |

## Síntomas
- {{Cómo se detecta: alerta, error visible, métrica.}}

## Impacto
{{Qué usuarios o funciones se ven afectados.}}

## Diagnóstico
1. Verificar salud: `GET /health/ready` → {{qué esperar}}.
2. Buscar en logs por `traceId` o por el error: {{consulta de ejemplo en el destino de logs}}.
3. {{Verificar dependencia X (BD, tercero) ...}}

## Mitigación inmediata
1. {{Acción segura y reversible para restablecer el servicio: reiniciar, escalar, desactivar feature flag, revertir despliegue.}}

## Solución definitiva
- {{Corrección de fondo y cómo verificarla.}}

## Escalamiento
| Cuándo | A quién | Cómo |
|---|---|---|
| {{No se mitiga en 30 min}} | {{Persona / rol}} | {{Canal}} |

## Historial
| Fecha | Incidente | Aprendizaje |
|---|---|---|
| {{AAAA-MM-DD}} | {{Resumen}} | {{Qué se cambió}} |
