# Reverse proxy con Nginx

> **Aplica a:** Todos los perfiles, solo si `Deployment = nginx` (servidor Linux o VM con Docker)  
> **Propósito:** Poner Nginx delante de la API: HTTPS con Let's Encrypt, límites en el borde, compresión, WebSockets y configuración correcta de la API detrás del proxy.  
> Índice general: `standards/00-INDEX.md`

## Cuándo usarlo (y cuándo no)
| Escenario | ¿Nginx? |
|---|---|
| Servidor Linux / VM propia con Docker | **Sí**: HTTPS, varias APIs por dominio, protección en el borde |
| Azure App Service o Container Apps | **No**: la plataforma ya trae proxy, HTTPS y balanceo |
| IIS en Windows | **No**: IIS cumple ese papel (ASP.NET Core Module) |
| Se prefiere todo en .NET | Alternativa: **YARP** (`ai/suggestions-catalog.md`) |
| Se quiere HTTPS automático sin certbot | Alternativa: **Caddy** |

## Arquitectura
```
Internet ──443/80──► nginx (TLS, límites, gzip) ──HTTP──► api:8080 (Kestrel) ──► base de datos
                         red interna "backend": la API y la BD no publican puertos
```

## La API detrás del proxy (obligatorio)
Ya está preparado en `standards/01a` (`ForwardedHeadersOptions` + `UseForwardedHeaders`). Solo se activa por configuración:

| Variable | Valor | Por qué |
|---|---|---|
| `ReverseProxy__Enabled` | `true` | Lee `X-Forwarded-For` / `-Proto` / `-Host` |
| `ReverseProxy__KnownNetworks__0` | Subred de la red Docker (ej. `172.30.0.0/24`) | Solo se confía en encabezados que vienen del proxy |

**Sin esto:**
- La API ve la IP de Nginx: el rate limiting por IP bloquearía a **todos** los clientes a la vez y los logs mostrarían la IP equivocada.
- La API cree que la petición llegó por HTTP: redirecciones y enlaces incorrectos.

`ForwardLimit = 1` hace que un cliente no pueda falsificar su IP mandando su propio `X-Forwarded-For`: la API solo toma la dirección que agregó Nginx.

**No duplicar trabajo entre Nginx y la API:**

| Tarea | Quién la hace |
|---|---|
| TLS / certificados | Nginx |
| Compresión gzip | Nginx (la API no habilita `ResponseCompression`) |
| Headers de seguridad, HSTS, ProblemDetails | API (`standards/09`); Nginx **no** los agrega |
| Rate limiting | Ambos: Nginx corta abusos gruesos; la API aplica sus reglas por IP y por endpoint |
| Tamaño máximo del body | Ambos, **con el mismo valor** (`client_max_body_size` = `MaxRequestBodySize` de Kestrel) |

## `deploy/nginx/nginx.conf`
```nginx
upstream api {
    server api:8080;
    keepalive 32;
}

# Límite en el borde por IP (la API aplica además sus propias reglas)
limit_req_zone $binary_remote_addr zone=api_limit:10m rate=20r/s;

# WebSockets/SignalR: Connection "upgrade" solo si el cliente lo pide; si no, vacío (mantiene el keepalive)
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      '';
}

server {
    listen 80;
    server_name {domain};

    location /.well-known/acme-challenge/ { root /var/www/certbot; }   # validación de Let's Encrypt
    location / { return 301 https://$host$request_uri; }
}

server {
    listen 443 ssl;
    http2 on;
    server_name {domain};

    ssl_certificate     /etc/letsencrypt/live/{domain}/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/{domain}/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_session_cache   shared:SSL:10m;

    server_tokens off;
    client_max_body_size  10m;    # = MaxRequestBodySize de Kestrel (standards/01a)
    client_body_timeout   15s;
    client_header_timeout 15s;

    gzip on;
    gzip_types application/json application/problem+json;
    gzip_min_length 1024;

    location / {
        limit_req zone=api_limit burst=40 nodelay;
        limit_req_status 429;

        proxy_pass         http://api;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        $connection_upgrade;
        proxy_read_timeout 60s;
        proxy_send_timeout 60s;
    }
}
```
`{domain}` es el dominio real (ej. `api.midominio.com`). Para probar en local sin certificados, usar un único `server { listen 80; ... }` con el mismo bloque `location /`.

## `docker-compose.yml` (producción en una VM)
```yaml
services:
  nginx:
    image: nginx:stable-alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./deploy/nginx/nginx.conf:/etc/nginx/conf.d/default.conf:ro
      - ./deploy/certbot/www:/var/www/certbot:ro
      - ./deploy/certbot/conf:/etc/letsencrypt:ro
    depends_on: [api]
    networks: [backend]
    restart: unless-stopped

  api:
    build: .
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "${DB_CONNECTION}"
      ReverseProxy__Enabled: "true"
      ReverseProxy__KnownNetworks__0: "172.30.0.0/24"
      Cors__AllowedOrigins__0: "https://{frontend-domain}"
      Jwt__SigningKey: "${JWT_SIGNING_KEY}"   # [SEC]
    expose: ["8080"]          # solo dentro de la red: no se publica al host
    networks: [backend]
    restart: unless-stopped

  # db: servicio de la base del perfil (standards/13), también sin publicar puertos en producción

networks:
  backend:
    ipam:
      config:
        - subnet: 172.30.0.0/24   # la misma subred que ReverseProxy__KnownNetworks__0
```
Las migraciones se aplican antes de levantar la versión nueva, con el bundle (`standards/13`), no al arrancar.

## Certificados (Let's Encrypt con certbot)
1. Levantar primero solo el `server` del puerto 80 (comentar el de 443) y emitir el certificado:
   ```bash
   docker run --rm -v ./deploy/certbot/conf:/etc/letsencrypt -v ./deploy/certbot/www:/var/www/certbot \
     certbot/certbot certonly --webroot -w /var/www/certbot -d {domain} --email {email} --agree-tos --no-eff-email
   ```
2. Activar el `server` 443 y recargar: `docker compose exec nginx nginx -s reload`.
3. Renovación: tarea programada (cron o systemd timer) que ejecute `certbot renew` con los mismos volúmenes y luego `nginx -s reload`. Los certificados de Let's Encrypt duran 90 días.

## Comandos útiles
```bash
docker compose exec nginx nginx -t           # validar la configuración antes de recargar
docker compose exec nginx nginx -s reload    # aplicar cambios sin cortar conexiones
docker compose logs -f nginx                 # access/error log
```

## Checklist
- [ ] `ReverseProxy__Enabled=true` y `KnownNetworks` = subred real de Docker. Verificar que la API registra la IP del cliente, no la de Nginx.
- [ ] La API y la base **no** publican puertos al host; solo Nginx expone 80/443.
- [ ] `client_max_body_size` igual al límite de Kestrel; timeouts acordes a `docs/04`.
- [ ] Renovación automática de certificados probada (`certbot renew --dry-run`).
- [ ] Nginx no agrega headers de seguridad ni HSTS (los pone la API).
- [ ] Si hay SignalR/WebSockets, probar la conexión a través del proxy.

## Referencias
- ASP.NET Core en Linux con Nginx: https://learn.microsoft.com/aspnet/core/host-and-deploy/linux-nginx
- ASP.NET Core detrás de proxies y balanceadores: https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer
- Documentación de Nginx: https://nginx.org/en/docs/
- Certbot: https://eff-certbot.readthedocs.io/
