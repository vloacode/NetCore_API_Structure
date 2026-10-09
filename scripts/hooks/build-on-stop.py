"""Hook Stop del kit NetCore_API_Structure.

Si hay archivos .cs/.csproj/.props modificados sin commitear y existe una solución, ejecuta `dotnet build`.
Si falla, devuelve código 2: Claude Code no termina el turno y recibe los errores para corregirlos
(Definition of Done: "dotnet build sin errores"). Evita bucles con `stop_hook_active`.
"""
import json
import pathlib
import shutil
import subprocess
import sys

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        data = {}

    if data.get("stop_hook_active"):
        sys.exit(0)   # ya se está corrigiendo por este hook: no encadenar

    root = pathlib.Path(data.get("cwd") or ".")
    solutions = sorted(root.glob("*.slnx")) + sorted(root.glob("*.sln"))
    if not solutions or not shutil.which("dotnet"):
        sys.exit(0)   # todavía no hay solución (kit sin inicializar) o no hay SDK

    try:
        status = subprocess.run(["git", "status", "--porcelain"], cwd=root,
                                capture_output=True, text=True, timeout=30).stdout
    except (OSError, subprocess.SubprocessError):
        status = ""

    changed_code = any(line.rstrip().endswith((".cs", ".csproj", ".props")) for line in status.splitlines())
    if not changed_code:
        sys.exit(0)

    result = subprocess.run(["dotnet", "build", str(solutions[0]), "-nologo", "-v", "q", "-clp:ErrorsOnly"],
                            cwd=root, capture_output=True, text=True, timeout=590)
    if result.returncode != 0:
        output = (result.stdout + "\n" + result.stderr).splitlines()
        errors = [line.strip() for line in output if "error" in line.lower()][:25]
        print("`dotnet build` falla. Corrige estos errores antes de terminar (Definition of Done):\n"
              + "\n".join(errors or output[-25:]), file=sys.stderr)
        sys.exit(2)

    sys.exit(0)


if __name__ == "__main__":
    main()
