"""Verificación estructural del kit NetCore_API_Structure (y de proyectos creados con él).

Comprueba:
  1. Enlaces relativos de Markdown que apunten a archivos inexistentes.
  2. Que cada subagente (.claude/agents) tenga frontmatter válido y su rol en ai/roles.
  3. Que cada skill (.claude/skills/<n>/SKILL.md) tenga frontmatter válido y su flujo en ai/workflows.
  4. Tamaño de los archivos de ai/ y standards/ (aviso > 300 líneas, error > 450) por modelos de contexto corto.
  5. Solo en el kit sin inicializar: que no queden nombres de proyectos o entidades de ejemplo (md y templates/).
  6. Que el código no vuelva a los md: bloques C# de más de 40 líneas en ai/, standards/ o docs/ son error
     (el código vive en templates/ y los standards lo enlazan).

Uso: python scripts/check-kit.py      (código de salida 1 si hay errores)
"""
import pathlib
import re
import sys

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parent.parent
errors: list[str] = []
warnings: list[str] = []


def md_files():
    for path in ROOT.rglob("*.md"):
        skip = {".git", "node_modules", "bin", "obj", ".verify"}
        if not skip.intersection(path.parts):
            yield path


def frontmatter(path: pathlib.Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---\n", text, re.DOTALL)
    if not match:
        return {}
    result = {}
    for line in match.group(1).splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            result[key.strip()] = value.strip()
    return result


def skip_build_output(path: pathlib.Path) -> bool:
    return bool({"bin", "obj"}.intersection(path.parts))


# 1. Enlaces
for md in md_files():
    for target in re.findall(r"\]\(([^)#\s]+)(?:#[^)]*)?\)", md.read_text(encoding="utf-8")):
        if target.startswith(("http://", "https://", "mailto:")):
            continue
        if not (md.parent / target).exists():
            errors.append(f"Enlace roto en {md.relative_to(ROOT)}: {target}")

# 2. Subagentes ↔ roles
for agent in sorted((ROOT / ".claude" / "agents").glob("*.md")):
    meta = frontmatter(agent)
    if not meta.get("name") or not meta.get("description"):
        errors.append(f"Frontmatter incompleto (name/description) en {agent.relative_to(ROOT)}")
    if not (ROOT / "ai" / "roles" / agent.name).exists():
        errors.append(f"El subagente {agent.name} no tiene rol en ai/roles/")
for role in sorted((ROOT / "ai" / "roles").glob("*.md")):
    if not (ROOT / ".claude" / "agents" / role.name).exists():
        warnings.append(f"El rol {role.name} no tiene subagente en .claude/agents/")

# 3. Skills ↔ flujos
for skill in sorted((ROOT / ".claude" / "skills").glob("*/SKILL.md")):
    meta = frontmatter(skill)
    name = skill.parent.name
    if meta.get("name") != name or not meta.get("description"):
        errors.append(f"Frontmatter inválido en {skill.relative_to(ROOT)} (name debe ser '{name}')")
    if not (ROOT / "ai" / "workflows" / f"{name}.md").exists():
        errors.append(f"La skill {name} no tiene flujo en ai/workflows/")
for workflow in sorted((ROOT / "ai" / "workflows").glob("*.md")):
    if not (ROOT / ".claude" / "skills" / workflow.stem / "SKILL.md").exists():
        warnings.append(f"El flujo {workflow.name} no tiene skill en .claude/skills/")

# 4. Tamaños
for folder in ("ai", "standards"):
    for md in sorted((ROOT / folder).rglob("*.md")):
        lines = len(md.read_text(encoding="utf-8").splitlines())
        if lines > 450:
            errors.append(f"{md.relative_to(ROOT)} tiene {lines} líneas (máximo 450): dividirlo")
        elif lines > 300:
            warnings.append(f"{md.relative_to(ROOT)} tiene {lines} líneas (objetivo ≤ 300)")

# 5. Nombres de ejemplo (solo en el kit, antes de project-init)
context = ROOT / "docs" / "00-MASTER_CONTEXT.md"
if context.exists() and "{{NombreProyecto}}" in context.read_text(encoding="utf-8"):
    forbidden = re.compile(r"\b(Keana|DoctorFinder|SampleApp|Products?(?![ -][Aa]nalyst)|Category|Categories)\b")
    for folder in ("ai", "standards", "docs"):
        for md in (ROOT / folder).rglob("*.md"):
            for number, line in enumerate(md.read_text(encoding="utf-8").splitlines(), 1):
                if forbidden.search(line):
                    errors.append(f"Nombre de ejemplo en {md.relative_to(ROOT)}:{number}: {line.strip()[:80]}")
    for source in (ROOT / "templates").rglob("*.cs"):
        if skip_build_output(source):
            continue
        for number, line in enumerate(source.read_text(encoding="utf-8").splitlines(), 1):
            if forbidden.search(line):
                errors.append(f"Nombre de ejemplo en {source.relative_to(ROOT)}:{number}: {line.strip()[:80]}")

# 6. Código largo dentro de los md
for folder in ("ai", "standards", "docs"):
    for md in sorted((ROOT / folder).rglob("*.md")):
        fence_open, is_csharp, start, count = False, False, 0, 0
        for number, line in enumerate(md.read_text(encoding="utf-8").splitlines(), 1):
            if line.startswith("```"):
                if not fence_open:
                    fence_open, is_csharp, start, count = True, line.startswith("```csharp"), number, 0
                else:
                    if is_csharp and count > 40:
                        errors.append(f"Bloque C# de {count} líneas en {md.relative_to(ROOT)}:{start}: moverlo a templates/ y enlazarlo")
                    fence_open = False
            elif fence_open:
                count += 1

for warning in warnings:
    print(f"AVISO  {warning}")
for error in errors:
    print(f"ERROR  {error}")
print(f"\n{len(errors)} error(es), {len(warnings)} aviso(s)")
sys.exit(1 if errors else 0)
