"""Pruebas de humo del perfil con seguridad. Uso: python smoke_secure.py <puerto> <archivo_log_de_la_api>
La ejecuta verify-standards.py --smoke."""
import base64, hashlib, hmac, json, re, struct, sys, time, urllib.request, urllib.error, pathlib

BASE = f"http://localhost:{sys.argv[1]}"
LOG = pathlib.Path(sys.argv[2])
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

results = []


def call(method, path, body=None, token=None, raw=False):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req) as r:
            text = r.read().decode()
            return r.status, (json.loads(text) if text else None), dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read().decode()
        try:
            payload = json.loads(text) if text else None
        except json.JSONDecodeError:
            payload = text
        return e.code, payload, dict(e.headers)


def check(name, ok, detail=""):
    results.append((ok, name, detail))
    print(("OK   " if ok else "FAIL ") + name + (f"  -> {detail}" if detail and not ok else ""))


def totp(secret_b32):
    key = base64.b32decode(secret_b32.upper() + "=" * (-len(secret_b32) % 8))
    counter = struct.pack(">Q", int(time.time()) // 30)
    digest = hmac.new(key, counter, hashlib.sha1).digest()
    offset = digest[-1] & 0x0F
    code = (struct.unpack(">I", digest[offset:offset + 4])[0] & 0x7FFFFFFF) % 1_000_000
    return f"{code:06d}"


# 1. Login admin
s, b, _ = call("POST", "/api/auth/login", {"email": "admin@verify.local", "password": "Admin#Pass12345"})
check("login admin 200 con tokens", s == 200 and b and b["tokens"]["accessToken"], f"{s} {b}")
admin = b["tokens"]["accessToken"]; admin_refresh = b["tokens"]["refreshToken"]

# 2. Perfil
s, b, _ = call("GET", "/api/account/me", token=admin)
check("GET /account/me devuelve rol Admin", s == 200 and "Admin" in b["roles"], f"{s} {b}")

# 2b. traceId consistente: 404 del framework con el mismo formato que X-Trace-Id
s, b, h = call("GET", "/api/no-existe", token=admin)
check("404 de ruta: traceId de 32 hex igual a X-Trace-Id",
      s == 404 and isinstance(b, dict) and len(b.get("traceId", "")) == 32 and b.get("traceId") == h.get("X-Trace-Id"), f"{s} {b} {h.get('X-Trace-Id')}")

# 3. Sin token -> 401
s, b, _ = call("GET", "/api/verifyitems")
check("GET /verifyitems sin token -> 401", s == 401, f"{s} {b}")

# 4. Validación -> 400 con code y claves camelCase
s, b, _ = call("POST", "/api/verifyitems", {"name": "", "code": "", "verifyParentId": 0}, token=admin)
check("POST inválido -> 400 Validation.Failed", s == 400 and b.get("code") == "Validation.Failed", f"{s} {b}")
check("errores con claves camelCase", s == 400 and "name" in b.get("errors", {}) and "verifyParentId" in b.get("errors", {}), f"{b}")
check("ProblemDetails con traceId", isinstance(b, dict) and bool(b.get("traceId")), f"{b}")

# 5. FK inexistente -> 400 VerifyItem.VerifyParentNotFound
s, b, _ = call("POST", "/api/verifyitems", {"name": "Uno", "code": "a-1", "verifyParentId": 999}, token=admin)
check("FK inexistente -> 400 VerifyItem.VerifyParentNotFound", s == 400 and b.get("code") == "VerifyItem.VerifyParentNotFound", f"{s} {b}")

# 6. Crear -> 201 con Location, DTO con fechas UTC (Z) y RowVersion
s, b, h = call("POST", "/api/verifyitems", {"name": "Uno", "code": "a-1", "verifyParentId": 1}, token=admin)
check("crear -> 201 con Location", s == 201 and "Location" in h, f"{s} {b} {h.get('Location')}")
item = b or {}
check("código normalizado a mayúsculas", item.get("code") == "A-1", f"{item}")
check("CreatedAt sale en UTC con Z", str(item.get("createdAt", "")).endswith("Z"), f"{item.get('createdAt')}")

# 7. Duplicado -> 409
s, b, _ = call("POST", "/api/verifyitems", {"name": "Otro", "code": "A-1", "verifyParentId": 1}, token=admin)
check("duplicado -> 409 VerifyItem.CodeAlreadyExists", s == 409 and b.get("code") == "VerifyItem.CodeAlreadyExists", f"{s} {b}")

# 8. Listado paginado
s, b, _ = call("GET", "/api/verifyitems?pageNumber=1&pageSize=500", token=admin)
check("listado paginado (pageSize recortado a 100)", s == 200 and b["totalCount"] == 1 and b["pageSize"] == 100, f"{s} {b}")

# 9. Update con RowVersion vieja -> 409; con la correcta -> 200
wid = item.get("id")
s, b, _ = call("PUT", f"/api/verifyitems/{wid}", {"name": "Uno editado", "isActive": True, "verifyParentId": 1,
                                             "rowVersion": "00000000-0000-0000-0000-000000000000"}, token=admin)
check("update con RowVersion vieja -> 409", s == 409 and b.get("code") == "VerifyItem.Concurrency", f"{s} {b}")
s, b, _ = call("PUT", f"/api/verifyitems/{wid}", {"name": "Uno editado", "isActive": True, "verifyParentId": 1,
                                             "rowVersion": item.get("rowVersion")}, token=admin)
check("update correcto -> 200 y UpdatedAt", s == 200 and b.get("name") == "Uno editado" and b.get("updatedAt"), f"{s} {b}")

# 10. Delete (soft) -> 204 y luego 404
s, _, _ = call("DELETE", f"/api/verifyitems/{wid}", token=admin)
check("delete -> 204", s == 204, f"{s}")
s, b, _ = call("GET", f"/api/verifyitems/{wid}", token=admin)
check("después del delete -> 404 VerifyItem.NotFound", s == 404 and b.get("code") == "VerifyItem.NotFound", f"{s} {b}")

# 11. Registro + confirmación (token desde el log del LoggingEmailSender) + login de usuario normal
s, _, _ = call("POST", "/api/auth/register", {"email": "user@verify.local", "password": "User#Pass12345", "firstName": "U"})
check("register -> 204", s == 204, f"{s}")
s, b, _ = call("POST", "/api/auth/login", {"email": "user@verify.local", "password": "User#Pass12345"})
check("login sin confirmar email -> 403 Auth.EmailNotConfirmed", s == 403 and b.get("code") == "Auth.EmailNotConfirmed", f"{s} {b}")
time.sleep(1)
log = LOG.read_text(encoding="utf-8", errors="ignore")
m = re.findall(r"confirm-email\?userId=([0-9a-f-]+)&amp;token=([A-Za-z0-9_-]+)", log) or re.findall(r"confirm-email\?userId=([0-9a-f-]+)&token=([A-Za-z0-9_-]+)", log)
check("email de confirmación en el log", bool(m), "no se encontró el enlace")
if m:
    user_id, token = m[-1]
    s, _, _ = call("POST", "/api/auth/confirm-email", {"userId": user_id, "token": token})
    check("confirm-email -> 204", s == 204, f"{s}")
s, b, _ = call("POST", "/api/auth/login", {"email": "user@verify.local", "password": "User#Pass12345"})
check("login usuario confirmado -> 200", s == 200, f"{s} {b}")
user_token = b["tokens"]["accessToken"] if s == 200 else None
s, b, _ = call("GET", "/api/verifyitems", token=user_token)
check("usuario sin permiso verifyitems.read -> 403", s == 403, f"{s} {b}")

# 12. Refresh con rotación y detección de reuso
s, b, _ = call("POST", "/api/auth/refresh", {"refreshToken": admin_refresh})
check("refresh -> 200 con refresh nuevo", s == 200 and b["refreshToken"] != admin_refresh, f"{s} {b}")
new_refresh = b["refreshToken"] if s == 200 else ""
s, b, _ = call("POST", "/api/auth/refresh", {"refreshToken": admin_refresh})
check("reuso de refresh viejo -> 401", s == 401, f"{s} {b}")
s, b, _ = call("POST", "/api/auth/refresh", {"refreshToken": new_refresh})
check("tras el reuso, la familia queda revocada -> 401", s == 401, f"{s} {b}")

# 13. 2FA: setup, enable con TOTP, login en dos pasos
s, b, _ = call("POST", "/api/account/2fa/setup", token=admin)
check("2fa setup -> clave y URI otpauth", s == 200 and b["authenticatorUri"].startswith("otpauth://totp/"), f"{s} {b}")
secret = b["sharedKey"].replace(" ", "") if s == 200 else ""
s, b, _ = call("POST", "/api/account/2fa/enable", {"code": totp(secret)}, token=admin)
check("2fa enable -> 10 códigos de recuperación", s == 200 and len(b["recoveryCodes"]) == 10, f"{s} {b}")
s, b, _ = call("POST", "/api/auth/login", {"email": "admin@verify.local", "password": "Admin#Pass12345"})
check("login con 2FA -> requiresTwoFactor", s == 200 and b["requiresTwoFactor"] and b["twoFactorToken"], f"{s} {b}")
challenge = b.get("twoFactorToken") if s == 200 else ""
s, b, _ = call("GET", "/api/account/me", token=challenge)
check("el token de 2FA NO sirve como access token -> 401", s == 401, f"{s}")
s, b, _ = call("POST", "/api/auth/login/2fa", {"twoFactorToken": challenge, "code": totp(secret)})
check("login/2fa con TOTP -> 200 tokens", s == 200 and b["accessToken"], f"{s} {b}")

# Analítica de uso (standards/17a): eventos del frontend
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "search.no_results", "properties": {"term_length": 7}}]})
check("analítica: evento válido del frontend -> 202", s == 202, f"{s} {b}")
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "admin.deleted_everything"}]})
check("analítica: evento fuera de la lista blanca -> 400", s == 400 and (b or {}).get("code") == "Validation.Failed", f"{s} {b}")
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "page.viewed", "properties": {"email": "a@b.com"}}]})
check("analítica: propiedad con datos personales -> 400", s == 400, f"{s} {b}")

# 14. Rate limiting de Auth (10/min por IP) -> 429 con Retry-After
codes = []
for _ in range(12):
    s, _, h = call("POST", "/api/auth/forgot-password", {"email": "nadie@verify.local"})
    codes.append(s)
    if s == 429:
        break
check("rate limit de Auth -> 429 con Retry-After", 429 in codes and "Retry-After" in h, f"{codes}")

failed = [r for r in results if not r[0]]
print(f"\n{len(results) - len(failed)}/{len(results)} OK")
sys.exit(1 if failed else 0)
