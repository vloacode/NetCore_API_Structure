"""Genera los adaptadores de cada herramienta de IA a partir de una sola fuente.

Fuente de verdad:
  - Flujos: ai/workflows/<flujo>.md  +  descripción en .claude/skills/<flujo>/SKILL.md (frontmatter)
  - Roles:  ai/roles/<rol>.md        +  descripción y herramientas en .claude/agents/<rol>.md (frontmatter)

Genera (archivos cortos que solo apuntan a ai/):
  .agents/skills/<flujo>/SKILL.md   estándar agentskills.io: Hermes Agent, opencode y otras IAs
  .opencode/commands/<flujo>.md     comandos /<flujo> en opencode
  .opencode/agents/<rol>.md         subagentes de opencode (los revisores, en solo lectura)

Uso:
  python scripts/sync-adapters.py          escribe los archivos
  python scripts/sync-adapters.py --check  no escribe; código 1 si algo está desactualizado (lo usa check-kit.py)
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent


def frontmatter(path: pathlib.Path) -> dict[str, str]:
    match = re.match(r"^---\n(.*?)\n---\n", path.read_text(encoding="utf-8"), re.DOTALL)
    result = {}
    for line in (match.group(1).splitlines() if match else []):
        if ":" in line:
            key, value = line.split(":", 1)
            result[key.strip()] = value.strip()
    return result


def yaml_quote(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def skill_md(name: str, description: str) -> str:
    return f"""---
name: {name}
description: {yaml_quote(description)}
---

# {name}

Flujo **{name}** del kit NetCore_API_Structure (APIs ASP.NET Core).

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` (en la raíz del proyecto) y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/{name}.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`; trabaja por fases (`ai/implementation-phases.md`).
4. Cuando el flujo nombre un rol, adopta las reglas de `ai/roles/<rol>.md` (o delega en el subagente de ese rol si tu herramienta los tiene).
5. Las decisiones de negocio se consultan siempre al usuario. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Todas las rutas son relativas a la raíz del proyecto (donde está `AGENTS.md`).
"""


def opencode_command(name: str, description: str) -> str:
    return f"""---
description: {yaml_quote(description)}
---
Ejecuta el flujo **{name}** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/{name}.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`.
4. Delega en los subagentes de `.opencode/agents/` que el flujo indique para cada rol. Las decisiones de negocio se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario: $ARGUMENTS
"""


def opencode_agent(name: str, description: str, read_only: bool) -> str:
    title = name.replace("-", " ").title()
    permission = "permission:\n  edit: deny\n  bash: ask\n" if read_only else ""
    rule = ("**Trabajas en solo lectura: no modifiques ningún archivo.** Usa la terminal solo para consultas "
            "(`dotnet build`, `dotnet test`, `git diff`, `dotnet list package --vulnerable`).\n\n") if read_only else ""
    return f"""---
description: {yaml_quote(description)}
mode: subagent
{permission}---
Eres el rol **{title}** del kit NetCore_API_Structure (APIs ASP.NET Core).

{rule}Antes de actuar, lee en este orden:
1. `AGENTS.md`: reglas, stack y Definition of Done.
2. `docs/00-MASTER_CONTEXT.md`: Perfil del proyecto (qué aplica).
3. `ai/roles/{name}.md`: tu definición completa (misión, alcance, reglas, entregables y checklist). Síguela al pie de la letra.
4. Solo los archivos que `ai/context-packs.md` indique para la tarea asignada.

Las decisiones de negocio no las tomas tú: devuélvelas como preguntas para el usuario.

Al terminar, responde con: qué hiciste y qué archivos tocaste; verificaciones (build, tests) y su resultado; pendientes, riesgos o preguntas.
"""


def expected_files() -> dict[pathlib.Path, str]:
    files: dict[pathlib.Path, str] = {}
    for workflow in sorted((ROOT / "ai" / "workflows").glob("*.md")):
        name = workflow.stem
        claude_skill = ROOT / ".claude" / "skills" / name / "SKILL.md"
        description = frontmatter(claude_skill).get("description") if claude_skill.exists() else None
        description = description or f"Flujo {name} del kit (ver ai/workflows/{name}.md)."
        files[ROOT / ".agents" / "skills" / name / "SKILL.md"] = skill_md(name, description)
        files[ROOT / ".opencode" / "commands" / f"{name}.md"] = opencode_command(name, description)
    for role in sorted((ROOT / "ai" / "roles").glob("*.md")):
        name = role.stem
        claude_agent = ROOT / ".claude" / "agents" / f"{name}.md"
        meta = frontmatter(claude_agent) if claude_agent.exists() else {}
        description = meta.get("description") or f"Rol {name} del kit (ver ai/roles/{name}.md)."
        tools = meta.get("tools", "")
        read_only = bool(tools) and "Edit" not in tools and "Write" not in tools
        files[ROOT / ".opencode" / "agents" / f"{name}.md"] = opencode_agent(name, description, read_only)
    return files


def main() -> None:
    check = "--check" in sys.argv
    expected = expected_files()
    managed_dirs = [ROOT / ".agents" / "skills", ROOT / ".opencode" / "commands", ROOT / ".opencode" / "agents"]
    stale = [p for d in managed_dirs if d.exists() for p in d.rglob("*.md") if p not in expected]
    outdated = [p for p, content in expected.items()
                if not p.exists() or p.read_text(encoding="utf-8") != content]

    if check:
        for path in outdated:
            print(f"Desactualizado: {path.relative_to(ROOT)}")
        for path in stale:
            print(f"Sobra (su flujo o rol ya no existe): {path.relative_to(ROOT)}")
        sys.exit(1 if outdated or stale else 0)

    for path, content in expected.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")
    for path in stale:
        path.unlink()
        if not any(path.parent.iterdir()):
            path.parent.rmdir()
    print(f"{len(expected)} adaptadores al día ({len(outdated)} escritos, {len(stale)} borrados)")


if __name__ == "__main__":
    main()
