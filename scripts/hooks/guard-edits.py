"""Hook PreToolUse (Edit/Write/MultiEdit) del kit NetCore_API_Structure.

1. Bloquea guardar secretos en appsettings*.json (standards/09).
2. En un proyecto ya inicializado, pide confirmación antes de editar archivos del kit
   (standards/, ai/, AGENTS.md): se actualizan con upgrade-kit o se registra un ADR.

Lee el evento JSON por stdin y responde con hookSpecificOutput. Sin dependencias externas.
"""
import json
import pathlib
import re
import sys

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")


def decide(decision: str, reason: str) -> None:
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason,
        }
    }))
    sys.exit(0)


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        sys.exit(0)

    tool_input = data.get("tool_input") or {}
    raw_path = tool_input.get("file_path") or ""
    if not raw_path:
        sys.exit(0)

    root = pathlib.Path(data.get("cwd") or ".").resolve()
    path = pathlib.Path(raw_path)
    try:
        rel = path.resolve().relative_to(root).as_posix()
    except ValueError:
        rel = path.as_posix()

    new_text = (tool_input.get("content") or "") + (tool_input.get("new_string") or "")
    for edit in tool_input.get("edits") or []:
        new_text += edit.get("new_string") or ""

    # 1. Secretos en appsettings
    if re.search(r"appsettings[^/]*\.json$", rel, re.IGNORECASE):
        secret_key = re.search(r'"(SigningKey|AdminPassword|ApiKey|ClientSecret|KeyHash)"\s*:\s*"[^"<{]+"', new_text)
        password = re.search(r"Password=(?!<)[^;\"]+", new_text, re.IGNORECASE)
        if secret_key or password:
            decide("deny", "No se guardan secretos en appsettings*.json. Usa `dotnet user-secrets` en desarrollo "
                           "y variables de entorno o Key Vault en producción (standards/09-security-baseline.md).")

    # 2. Archivos del kit en un proyecto inicializado
    context = root / "docs" / "00-MASTER_CONTEXT.md"
    initialized = context.exists() and "{{NombreProyecto}}" not in context.read_text(encoding="utf-8", errors="ignore")
    kit_file = rel.startswith(("standards/", "ai/")) or rel in ("AGENTS.md", "CLAUDE.md")
    if initialized and kit_file:
        decide("ask", f"'{rel}' es un archivo del kit. En un proyecto no se edita directamente: se actualiza con "
                      "upgrade-kit o el desvío se registra en un ADR (docs/decisions/). ¿Confirmas el cambio?")

    sys.exit(0)


if __name__ == "__main__":
    main()
