# Instrucciones para la AI

Este proyecto es (o será) una ASP.NET Core Web API que sigue **obligatoriamente** la arquitectura descrita en
[AI_GUIDE_API_ARCHITECTURE.md](AI_GUIDE_API_ARCHITECTURE.md). Léela completa antes de crear o modificar código.

- Antes de generar código, confirma con el usuario las decisiones de la sección **0.1** de la guía.
- Las piezas de infraestructura (secciones 3–7) y de autenticación (sección 8) se implementan tal cual, cambiando solo `{Project}`.
- Las entidades de negocio **no existen en la guía**: créalas con las plantillas de la sección **9** a partir de lo que pida el usuario. No crear entidades de ejemplo.
- Respeta las reglas no negociables de la sección **0.2** y usa el checklist de la sección **11** por cada entidad nueva.
- Si una API de librería no coincide con la versión instalada (p. ej. Microsoft.OpenApi), ajusta el código a la versión real y avisa al usuario.
