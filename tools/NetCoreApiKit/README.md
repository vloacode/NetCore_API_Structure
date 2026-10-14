# NetCoreApiKit

Asistente paso a paso para crear APIs ASP.NET Core (.NET 10) con el kit [NetCore_API_Structure](https://github.com/vloacode/NetCore_API_Structure).

```bash
dnx NetCoreApiKit
```

Pregunta el nombre, el tipo de API (con usuarios y login JWT, o pública), la base de datos (PostgreSQL o SQL Server; en Docker, instalada en la máquina o en un servidor), los emails, las capacidades extra y dónde guardar los secretos. Después genera la solución, copia la guía para IA, levanta la base, guarda los secretos, compila y crea la migración inicial.

Sin preguntas (IA, CI):

```bash
dnx NetCoreApiKit -- --name MiApi --security --database postgresql --email smtp --db-location docker --yes
```

Requisitos: .NET 10 SDK; Docker si la base va en un contenedor.
