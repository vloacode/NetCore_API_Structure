# Integraciones, eventos, notificaciones y jobs

<!-- IA: todo lo que la API hace "hacia afuera" o "en segundo plano". Si una sección no aplica, escribir "No aplica".
Implementación: ai/suggestions-catalog.md (webhooks, outbox, jobs, email, SignalR) y standards/11 (HttpClient resiliente). -->

## Integraciones externas
| Sistema | Dirección | Protocolo | Propósito | Autenticación | Si falla |
|---|---|---|---|---|---|
| {{Pasarela de pagos}} | {{Saliente / Entrante}} | {{REST / webhook / SFTP}} | {{Cobrar ...}} | {{API key en Key Vault}} | {{Reintentar 3 veces y marcar como pendiente}} |

### {{Sistema}}
- Documentación del proveedor: {{URL}}
- Cliente: `I{{Sistema}}Client` (HttpClient tipado con `AddStandardResilienceHandler`).
- Configuración: `{{Sistema}}:BaseUrl`; secretos: `{{Sistema}}:ApiKey` (user-secrets / Key Vault).
- Datos que se envían y reciben: {{...}}

## Eventos de dominio
<!-- IA: "cuando pasa X, hay que hacer Y". Si Y es crítico (no puede perderse), usar outbox. -->
| Evento | Cuándo ocurre | Consecuencias | ¿Garantía requerida? |
|---|---|---|---|
| `{{Entity}}Created` | {{Al crear ...}} | {{Enviar email, notificar al sistema X}} | {{Sí → outbox / No}} |

## Notificaciones
| Notificación | Canal | Destinatario | Disparador | Plantilla |
|---|---|---|---|---|
| Confirmación de cuenta `[SEC]` | Email | Usuario | Registro | `AccountEmails.SendConfirmationAsync` |
| {{Notificación}} | {{Email / SignalR / SMS / push}} | {{Quién}} | {{Evento}} | {{Nombre}} |

## Jobs en segundo plano
| Job | Frecuencia | Qué hace | Implementación | Si falla |
|---|---|---|---|---|
| Limpieza de refresh tokens `[SEC]` | Diario 03:00 | Borra tokens expirados o revocados con más de 30 días | `BackgroundService` + `ExecuteDeleteAsync` | Se reintenta al día siguiente; alerta si falla 3 veces |
| {{Job}} | {{Cron}} | {{...}} | {{BackgroundService / Hangfire}} | {{...}} |
