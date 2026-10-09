# Despliegue y producción

> **Aplica a:** Todos los perfiles  
> **Propósito:** Checklist de producción.  
> Índice general: `standards/00-INDEX.md`

## Producción: checklist

- [ ] **Llaves de Data Protection persistentes**. Firman los tokens de email/reset; si se pierden al reiniciar o hay varias instancias, los enlaces dejan de servir. Ejemplo: `services.AddDataProtection().PersistKeysToDbContext<AppDbContext>()` (paquete `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) o Azure Blob/Key Vault.
- [ ] `Jwt:SigningKey` en Key Vault o variables de entorno, con rotación planificada.
- [ ] `IEmailSender` real (SMTP con MailKit, SendGrid, etc.) y plantillas HTML.
- [ ] Job programado (`BackgroundService` o Hangfire) que borre `RefreshTokens` expirados o revocados con más de N días: `ExecuteDeleteAsync`.
- [ ] `ApplyMigrationsOnStartup = false`. Las migraciones van en el pipeline de despliegue (`dotnet ef migrations bundle`).
- [ ] CORS solo con los orígenes reales. HTTPS y HSTS activos.
- [ ] Límite de peticiones en Auth ajustado. Si hay proxy o load balancer, configurar `ForwardedHeaders` para obtener la IP real.
- [ ] Logs estructurados (Serilog/OpenTelemetry) sin datos sensibles (nunca contraseñas, tokens ni códigos 2FA).
- [ ] Health checks (`AddHealthChecks().AddDbContextCheck<AppDbContext>()`).
