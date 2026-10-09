"""Pruebas de humo del perfil público. Uso: python smoke_public.py <puerto> [log]
La ejecuta verify-standards.py --smoke."""
import json, sys, urllib.request, urllib.error

BASE = f"http://localhost:{sys.argv[1]}"
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

results = []


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as r:
            text = r.read().decode()
            try:
                return r.status, (json.loads(text) if text else None), dict(r.headers)
            except json.JSONDecodeError:
                return r.status, text, dict(r.headers)
    except urllib.error.HTTPError as e:
        text = e.read().decode()
        try:
            return e.code, (json.loads(text) if text else None), dict(e.headers)
        except json.JSONDecodeError:
            return e.code, text, dict(e.headers)


def check(name, ok, detail=""):
    results.append(ok)
    print(("OK   " if ok else "FAIL ") + name + (f"  -> {detail}" if detail and not ok else ""))


s, b, h = call("GET", "/health/ready")
check("health ready 200 con headers de seguridad", s == 200 and h.get("X-Content-Type-Options") == "nosniff", f"{s} {h}")
s, b, _ = call("POST", "/api/auth/login", {"email": "x@x.com", "password": "x"})
check("sin endpoints de Auth (404)", s == 404, f"{s}")
s, b, _ = call("POST", "/api/verifyitems", {"name": "", "code": "", "verifyParentId": 0})
check("validación -> 400 Validation.Failed", s == 400 and b.get("code") == "Validation.Failed", f"{s} {b}")
s, b, h = call("POST", "/api/verifyitems", {"name": "Público", "code": "p-1", "verifyParentId": 1})
check("crear anónimo -> 201", s == 201 and "Location" in h, f"{s} {b}")
wid = (b or {}).get("id")
s, b, _ = call("GET", "/api/verifyitems?pageNumber=1&pageSize=10")
check("listado anónimo -> 200 con 1 registro", s == 200 and b["totalCount"] == 1, f"{s} {b}")
s, _, _ = call("DELETE", f"/api/verifyitems/{wid}")
check("delete -> 204", s == 204, f"{s}")
s, b, h = call("GET", f"/api/verifyitems/{wid}")
check("después del delete -> 404 con traceId = X-Trace-Id", s == 404 and b.get("traceId") == h.get("X-Trace-Id"), f"{s} {b}")

# Analítica de uso (standards/17a): eventos del frontend
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "search.no_results", "properties": {"term_length": 7}}]})
check("analítica: evento válido del frontend -> 202", s == 202, f"{s} {b}")
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "admin.deleted_everything"}]})
check("analítica: evento fuera de la lista blanca -> 400", s == 400 and (b or {}).get("code") == "Validation.Failed", f"{s} {b}")
s, b, _ = call("POST", "/api/analytics/events", {"events": [{"name": "page.viewed", "properties": {"email": "a@b.com"}}]})
check("analítica: propiedad con datos personales -> 400", s == 400, f"{s} {b}")

codes = []
for _ in range(110):
    s, _, h = call("GET", "/api/verifyitems")
    codes.append(s)
    if s == 429:
        break
check("límite global por IP -> 429 con Retry-After", 429 in codes and "Retry-After" in h, f"últimos: {codes[-3:]}")

print(f"\n{sum(results)}/{len(results)} OK")
sys.exit(0 if all(results) else 1)
