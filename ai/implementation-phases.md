# Implementación por fases

Objetivo: que la IA **nunca cargue todo el kit de una vez**. El proyecto se construye en fases cortas. Cada fase dice qué leer, qué producir y cómo comprobar que quedó bien. Al terminar una fase se actualiza `docs/ai/PROJECT_STATUS.md` y se puede cerrar la sesión: la siguiente (otra IA, otro día, un modelo local) retoma desde ahí leyendo solo el estado y el pack de la fase.

## Regla de oro
1. Al empezar una sesión: leer `AGENTS.md` → `docs/ai/PROJECT_STATUS.md` (fase actual y siguiente paso) → **solo** los archivos de esa fase.
2. Trabajar una fase (o un ítem de la fase) a la vez. No adelantar trabajo de fases siguientes.
3. Cerrar la fase con su **puerta de verificación** en verde, actualizar `PROJECT_STATUS.md` y hacer commit (si el usuario lo autorizó).
4. Si el contexto se llena a mitad de fase: guardar el avance en `PROJECT_STATUS.md` ("En curso" + siguiente paso exacto) y seguir en otra sesión.

## Fases

| Fase | Qué se hace | Leer (además de `AGENTS.md` y `PROJECT_STATUS`) | Produce | Puerta de verificación |
|---|---|---|---|---|
| **0. Perfil y documentación** | Modo, entrevista, sugerencias, docs del proyecto (`project-init` pasos 1–5) | `ai/workflows/project-init.md`, `ai/suggestions-catalog.md`, plantillas de `docs/` | `docs/00`–`10`, módulos, ADR-0001, **plan de fases** en `PROJECT_STATUS` | Usuario aprueba el resumen; sin `{{` en `docs/` |
| **1. Esqueleto** | `dotnet new kitapi` con el perfil, secretos, migración inicial | `standards/01-solution-architecture.md` (secciones "Perfil" y "Crear el proyecto") | Solución compilando con Auth (si aplica) y health checks | `dotnet build -c Release` y `dotnet test` en verde |
| **2. Dominio por módulo** (una vuelta por módulo) | Entidades del módulo **una a una**, en orden de dependencia (padres primero), con sus reglas y tests | `docs/modules/<módulo>.md`, `ai/workflows/new-entity.md`, `standards/07-entities.md` (+ `06` con seguridad) | `Features/{Entities}/` por entidad, migración, tests | Por entidad: build + test en verde; por módulo: endpoints del módulo documentados |
| **3. Capacidades** (una vuelta por capacidad) | Cada capacidad aceptada (caché, outbox, archivos, jobs, analítica…), una a la vez | La entrada de la capacidad en `ai/suggestions-catalog.md` y el standard que indique | Capacidad funcionando con su test | Build + test en verde; ADR si cambia una decisión |
| **4. Endurecimiento** | Revisión de seguridad y rendimiento, tests faltantes | `ai/workflows/security-review.md`, `performance-review.md`, `test-generation.md` | Hallazgos corregidos o registrados | Checklist de `standards/09` completo |
| **5. Despliegue** | Docker, CI/CD, entorno de producción (Nginx si aplica) | `standards/13-deployment.md` (+ `13a`) | Pipeline y checklist de producción | CI en verde; checklist de `standards/13` |

## Plan de fases en `PROJECT_STATUS.md`
En la fase 0 se escribe el plan concreto del proyecto, por ejemplo:

| # | Fase | Ítems | Estado |
|---|---|---|---|
| 1 | Esqueleto | `kitapi --security true --database postgresql` | ✅ |
| 2a | Módulo Clientes | `Customer` → `Address` | 🔄 `Address` en curso |
| 2b | Módulo Facturación | `Invoice` (padre `Customer`) → `InvoiceLine` | ⏳ |
| 3 | Capacidades | caché de catálogos; product-analytics | ⏳ |
| 4 | Endurecimiento | — | ⏳ |
| 5 | Despliegue | Docker + Nginx | ⏳ |

## Tamaño de una sesión (guía para modelos con poco contexto)
- Fase 1: una sesión.
- Fase 2: **una entidad por sesión** (generar, adaptar, registrar, migración, tests, docs).
- Fase 3: una capacidad por sesión.
- Si una entidad tiene muchas reglas de negocio: primero el CRUD verde, después cada regla en su propia sesión.
