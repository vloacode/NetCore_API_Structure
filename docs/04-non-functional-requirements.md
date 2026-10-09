# Requerimientos no funcionales

<!-- IA: valores concretos y medibles. Si el usuario no los da, proponer los valores por defecto de esta plantilla,
marcarlos "(propuesto)" y confirmarlos. Estos números los usan standards/09, 10, 11 y 13. -->

## Rendimiento
| Métrica | Objetivo | Por defecto |
|---|---|---|
| Latencia p95 de lecturas | {{300 ms}} | 300 ms |
| Latencia p95 de escrituras | {{500 ms}} | 500 ms |
| Usuarios concurrentes esperados | {{100}} | — |
| Peticiones por minuto (pico) | {{1 000}} | — |
| Volumen de datos a 12 meses | {{Ej.: 1 M de registros en la tabla principal}} | — |

## Disponibilidad y recuperación
| Métrica | Objetivo |
|---|---|
| Disponibilidad mensual | {{99.5 %}} |
| RPO (pérdida máxima de datos) | {{24 h}} |
| RTO (tiempo máximo de recuperación) | {{4 h}} |
| Ventana de mantenimiento | {{Domingos 02:00–04:00 (hora local)}} |

## Seguridad
- Perfil: {{con seguridad / API pública}} (ver Perfil en `00-MASTER_CONTEXT.md`).
- Rate limiting: global {{100}} peticiones/min por IP; Auth {{10}}/min.
- Duración de tokens `[SEC]`: access {{15}} min, refresh {{7}} días.
- Política de contraseñas `[SEC]`: {{mínimo 8, mayúscula, minúscula, número y símbolo}}.
- Requisitos normativos: {{Ninguno / Ley de protección de datos local / HIPAA / PCI-DSS …}}.

## Privacidad y retención de datos
| Dato | Sensibilidad | Retención | Acción al vencer |
|---|---|---|---|
| {{Datos personales de clientes}} | {{Alta}} | {{5 años tras la última actividad}} | {{Anonimizar}} |
| Refresh tokens `[SEC]` | Alta | 30 días tras expirar o revocarse | Borrado físico (job) |
| Logs | Media | {{30 días}} | Borrado automático en el destino de logs |

## Escalabilidad
- {{Instancias previstas (1 / varias); si son varias, Data Protection persistente y caché distribuida.}}

## Observabilidad
- Destino de logs y trazas: {{Application Insights / Seq / Grafana / archivo}}.
- Alertas obligatorias: las de `standards/10-observability.md` con los umbrales de este documento.

## Compatibilidad
- Consumidores: {{navegadores modernos vía frontend propio / app móvil / sistemas externos}}.
- Versionado de API: {{no / sí (/api/v1)}}.
- Idioma de mensajes: {{español}}. Zona horaria de operación del negocio: {{America/Puerto_Rico}}.
