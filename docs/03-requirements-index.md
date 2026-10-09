# Índice de requerimientos y trazabilidad

<!-- IA: índice, no detalle. El detalle de cada FR está en su módulo. Este archivo responde:
"¿qué endpoint y qué test cubren cada requerimiento?" y "¿qué falta?". Lo actualiza docs-sync tras cada cambio. -->

## Convenciones
- Requerimiento funcional: `FR-<MOD>-001`, donde `<MOD>` es un código de 3 o 4 letras del módulo (ej. `FR-INV-001`).
- Estado: `Pendiente`, `En curso`, `Implementado`, `Probado`.
- Endpoint con método y ruta: `POST /api/{{entities}}`.
- Test con el nombre de la clase y el método: `{{Entities}}ApiTests.Create_WithDuplicateCode_Returns409WithCode`.

## Trazabilidad

| FR | Descripción corta | BR | Módulo | Endpoint(s) | Permiso | Test(s) | Estado |
|---|---|---|---|---|---|---|---|
| FR-{{MOD}}-001 | {{Crear ...}} | BR-001 | {{Modulo}} | `POST /api/{{entities}}` | `{{entities}}.write` | `{{Clase.Metodo}}` | {{Pendiente}} |

## Cobertura
| Módulo | FR totales | Implementados | Probados |
|---|---|---|---|
| {{Modulo}} | {{0}} | {{0}} | {{0}} |

## Requerimientos sin cubrir
<!-- IA: lista generada al revisar la tabla: FR sin endpoint o sin test. -->
- {{Ninguno}}
