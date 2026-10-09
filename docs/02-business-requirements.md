# Requerimientos de negocio

<!-- IA: requerimientos expresados como necesidad del negocio, no como funcionalidad técnica.
Cada uno con id BR-xxx estable (nunca se reutiliza un id). Su desglose en requerimientos funcionales (FR) vive en
docs/modules/<modulo>.md, y la trazabilidad en 03-requirements-index.md. -->

## Convenciones
- Id: `BR-001`, `BR-002`… Nunca se renumera; si uno se descarta, se marca `Descartado`.
- Prioridad (MoSCoW): `Must`, `Should`, `Could`, `Won't` (esta versión).
- Estado: `Propuesto`, `Aprobado`, `Implementado`, `Descartado`.

## Requerimientos

| Id | Requerimiento | Actor | Prioridad | Estado | Módulos |
|---|---|---|---|---|---|
| BR-001 | {{El negocio necesita registrar ... para ...}} | {{Actor}} | {{Must}} | {{Aprobado}} | {{Modulo}} |

## Detalle
<!-- IA: un bloque por BR solo si necesita contexto adicional. -->

### BR-001 — {{Título corto}}
- **Necesidad:** {{Por qué el negocio lo necesita.}}
- **Criterios de aceptación de negocio:**
  - {{Condición verificable desde el punto de vista del usuario.}}
- **Reglas de negocio relacionadas:** {{Ej.: "un cliente no puede tener dos facturas abiertas"}}
- **Notas / preguntas abiertas:** {{...}}
