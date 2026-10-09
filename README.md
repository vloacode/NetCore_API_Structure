# NetCore_API_Structure
Con este repositorio podemos dejar que la AI realice todo el setup inicial del proyecto.

## Contenido

| Archivo | Para qué sirve |
|---|---|
| [AI_GUIDE_API_ARCHITECTURE.md](AI_GUIDE_API_ARCHITECTURE.md) | Guía completa de arquitectura: ASP.NET Core Web API (.NET 10 LTS), Repository + Unit of Work + Specification, `Result<T>`, auditoría/soft delete automáticos, Identity local con JWT + refresh tokens, 2FA, administración de usuarios/roles y permisos. Las entidades de negocio van como plantillas con marcadores (`{Entity}`). |
| [CLAUDE.md](CLAUDE.md) | Instrucciones que Claude Code carga automáticamente cuando trabaja en un proyecto que incluye este archivo. |

## Cómo usarlo en un proyecto nuevo

1. Copiar `AI_GUIDE_API_ARCHITECTURE.md` y `CLAUDE.md` a la raíz del proyecto nuevo (o pegarle el enlace de este repositorio a la AI).
2. Pedirle a la AI algo como:
   > Crea el proyecto `{NombreProyecto}` siguiendo AI_GUIDE_API_ARCHITECTURE.md. Las entidades son: ...
3. La AI confirmará primero las decisiones de la sección 0.1 de la guía (nombre, entidades, roles, email, URLs) y luego generará la estructura.
