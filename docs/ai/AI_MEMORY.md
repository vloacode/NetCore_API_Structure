# Memoria del proyecto para la IA

<!-- IA: memoria persistente y portable entre herramientas y sesiones. Leer al empezar tareas grandes.
Agregar una entrada cuando: el usuario corrige algo, se descubre una trampa, algo tomó varios intentos, o se establece una preferencia.
Formato fijo, una línea por entrada, la más reciente arriba de cada sección. Máximo ~150 líneas: si crece, consolidar.
NO duplicar lo que ya está en standards, en ADRs o en el código. -->

## Preferencias del usuario
<!-- Formato: - AAAA-MM-DD · preferencia · (origen: corrección / pedido explícito) -->
- {{AAAA-MM-DD · Ej.: los mensajes de error se escriben de "usted" · pedido explícito}}

## Trampas y lecciones del proyecto
<!-- Formato: - AAAA-MM-DD · síntoma → causa → cómo evitarlo -->
- {{AAAA-MM-DD · Ej.: la migración X falló en producción → índice único con datos duplicados → limpiar datos antes de crear índices únicos}}

## Contexto que no está en el código
<!-- Hechos del negocio o del entorno que una IA no puede deducir. -->
- {{Ej.: el sistema externo X solo acepta llamadas de 8:00 a 18:00 hora local.}}

## Comandos y atajos verificados
- {{Ej.: `dotnet ef database update -p src/{Project}.Api -- --environment Development`}}
