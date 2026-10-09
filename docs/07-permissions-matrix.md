# Matriz de permisos

<!-- IA: solo aplica si Security = enabled. Si el perfil es API pública, dejar solo la sección "Perfil público".
Es la fuente de verdad de QUÉ rol puede hacer QUÉ. El código (Permissions.cs, DefaultUserPermissions, seed) debe coincidir.
Lo actualiza new-entity (nuevos permisos) y docs-sync. -->

## Roles

| Rol | Descripción | Sistema |
|---|---|---|
| Admin | Acceso total. Pasa todas las políticas | Sí |
| User | Usuario registrado estándar | Sí |
| {{Rol}} | {{Descripción}} | No |

## Permisos por rol

| Permiso | Descripción | Admin | User | {{Rol}} |
|---|---|---|---|---|
| `users.read` | Ver usuarios | ✔ | | |
| `users.manage` | Crear, bloquear, asignar roles | ✔ | | |
| `roles.read` | Ver roles y permisos | ✔ | | |
| `roles.manage` | Gestionar roles y permisos | ✔ | | |
| `{{entities}}.read` | {{Ver ...}} | ✔ | {{✔}} | {{✔}} |
| `{{entities}}.write` | {{Crear y editar ...}} | ✔ | | {{✔}} |
| `{{entities}}.delete` | {{Eliminar ...}} | ✔ | | |

## Reglas de acceso a datos (además del permiso)
<!-- IA: reglas de "dueño" (API1/BOLA, standards/09). Se implementan en la spec, no en el controller. -->
| Recurso | Regla |
|---|---|
| {{Entity}} | {{Un User solo ve los registros que creó / de su empresa}} |

## Perfil público (Security = disabled)
| Endpoint o grupo | Acceso |
|---|---|
| {{GET /api/{{entities}}}} | {{Anónimo}} |
| {{POST/PUT/DELETE /api/{{entities}}}} | {{API key / no expuesto}} |
