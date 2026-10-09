# Estado del proyecto

<!-- IA: tablero de avance. Se actualiza al final de CADA tarea (Definition of Done). Lo primero que mira una IA
para saber dónde quedó el trabajo y qué leer a continuación (ai/implementation-phases.md). Corto y factual;
el historial detallado está en git. -->

**Última actualización:** {{AAAA-MM-DD}} · **Kit:** {{3.0.0}} · **Fase actual:** {{2a — Módulo X}}

## Siguiente paso exacto
<!-- IA: una sola acción concreta, para que otra sesión pueda retomarla sin leer nada más que su pack. -->
{{Crear `Address` con kit-entity (padre `Customer`) — pack: docs/modules/clientes.md + standards/07-entities.md}}

## Plan de fases
<!-- IA: se escribe en project-init (paso 6). Estados: ✅ hecho · 🔄 en curso · ⏳ pendiente · ⏸ pospuesto. -->
| # | Fase | Ítems (en orden) | Estado |
|---|---|---|---|
| 0 | Perfil y documentación | entrevista, docs, ADR-0001 | {{✅}} |
| 1 | Esqueleto | `dotnet new kitapi {{opciones del perfil}}`, migración inicial | {{✅}} |
| 2a | {{Módulo}} | {{Entidad padre → entidad hija}} | {{🔄}} |
| 3 | Capacidades | {{capacidad 1; capacidad 2}} | {{⏳}} |
| 4 | Endurecimiento | revisión de seguridad y rendimiento | {{⏳}} |
| 5 | Despliegue | {{Docker / CI / Nginx}} | {{⏳}} |

## Hecho
| Fecha | Qué | Referencia |
|---|---|---|
| {{AAAA-MM-DD}} | {{Proyecto inicializado (modo 1, con seguridad)}} | {{ADR-0001}} |

## En curso
| Qué | Responsable | Siguiente paso |
|---|---|---|
| {{Módulo X: entidades}} | {{IA / persona}} | {{Crear {{Entity}} con new-entity}} |

## Pendiente (siguiente)
1. {{...}}

## Bloqueos y preguntas abiertas
- {{Ninguno / Q-1 en 10-roadmap.md}}

## Salud técnica
| Chequeo | Estado | Fecha |
|---|---|---|
| `dotnet build` | {{OK}} | {{AAAA-MM-DD}} |
| `dotnet test` | {{OK · N tests}} | {{AAAA-MM-DD}} |
| Migraciones aplicadas | {{InitialCreate}} | {{AAAA-MM-DD}} |
| Paquetes vulnerables | {{Ninguno}} | {{AAAA-MM-DD}} |
