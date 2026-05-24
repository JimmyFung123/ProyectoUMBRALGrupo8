# UMBRAL — Guía de incorporación al proyecto

## ¿Qué es UMBRAL?

Plataforma educativa de juegos interactivos en tiempo real. El operador crea misiones con etapas (trivia y búsqueda del tesoro), abre sesiones, y los participantes compiten en equipo desde sus celulares.

---

## Estructura del repositorio

```
ProyectoUMBRAL_Grupo8/
├── UMBRAL_Back-end/
│   ├── SessionService/        # Puerto 5092 — orquestador principal
│   ├── TeamService/           # Puerto 5095 — equipos y puntaje
│   ├── ClueService/           # Puerto 5094 — pistas
│   ├── StageService/          # Puerto 5093 — etapas de misiones
│   ├── MissionService/        # Puerto 5091 — misiones
│   └── UMBRAL_Back-end.Tests/ # xUnit + Moq + FluentAssertions
├── UMBRAL_Front-end/          # Puerto 5173 — operador/administrador (React 19 + Vite)
└── UMBRAL_Front-end_Participantes/  # Puerto 5174 — participantes, mobile-first (React 19 + Vite)
```

---

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 10, C# |
| ORM | EF Core 10 + Npgsql (PostgreSQL) |
| Mediator | MediatR (CQRS) |
| Mensajería | MassTransit + RabbitMQ |
| Tiempo real | SignalR |
| Tests | xUnit + Moq + FluentAssertions |
| Frontend operador | React 19 + TypeScript + Vite (puerto 5173) |
| Frontend participante | React 19 + TypeScript + Vite (puerto 5174) |

---

## Cómo levantar el proyecto

### Backend (cada servicio en su propia terminal)

```bash
cd UMBRAL_Back-end/SessionService && dotnet run
cd UMBRAL_Back-end/TeamService    && dotnet run
cd UMBRAL_Back-end/ClueService    && dotnet run
cd UMBRAL_Back-end/StageService   && dotnet run
cd UMBRAL_Back-end/MissionService && dotnet run
```

### Migraciones (primera vez o cuando haya cambios de schema)

```bash
cd UMBRAL_Back-end/SessionService && dotnet ef database update
cd UMBRAL_Back-end/TeamService    && dotnet ef database update
cd UMBRAL_Back-end/ClueService    && dotnet ef database update
cd UMBRAL_Back-end/StageService   && dotnet ef database update
cd UMBRAL_Back-end/MissionService && dotnet ef database update
```

### Frontend

```bash
# Operador
cd UMBRAL_Front-end && npm install && npm run dev

# Participante
cd UMBRAL_Front-end_Participantes && npm install && npm run dev
```

---

## Arquitectura y patrones

### Arquitectura hexagonal por servicio

```
Adapter/Controllers/     ← entrada HTTP (ASP.NET Controllers)
Application/             ← casos de uso (Commands + Queries via MediatR)
Domain/                  ← entidades, errores, interfaces de repositorio
Infrastructure/          ← EF Core, HttpClients, SignalR, BackgroundServices
```

### CQRS con MediatR

- **Commands** → modifican estado → `IRequest<Result<T>>`
- **Queries** → solo lectura → `IRequest<T>` o `IRequest<Result<T>>`

```csharp
// Command
public record PenalizeTeamCommand(Guid SessionId, Guid TeamId, int Points, string Reason)
    : IRequest<Result<int>>;

// Handler
public class PenalizeTeamCommandHandler : IRequestHandler<PenalizeTeamCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PenalizeTeamCommand request, CancellationToken ct) { ... }
}
```

### Patrón Result

Nunca se lanzan excepciones de negocio. Siempre se retorna `Result<T>`:

```csharp
// Éxito
return Result.Success(value);

// Fallo
return Result.Failure<T>(DomainErrors.SomeError);

// En el controller
return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
```

### Errores de dominio

```csharp
// Domain/Teams/TeamErrors.cs
public static class TeamErrors
{
    public static readonly Error NotFound = new("Team.NotFound", "Team not found.");
    public static readonly Error TeamFull  = new("Team.Full", "Team is full.");
}
```

### Comunicación entre servicios

SessionService orquesta al resto vía HTTP:

```csharp
// Interface (puerto/aplicación)
public interface ITeamServiceClient
{
    Task<int> PenalizeTeamAsync(Guid teamId, int points, string reason, CancellationToken ct);
}

// Implementación (infraestructura)
// SessionService/Infrastructure/ExternalClients/TeamServiceClient.cs
```

CORS configurado para 5173 y 5174 en cada servicio.

---

## Cómo implementar una Historia de Usuario nueva

Seguimos este flujo para cada HU:

### 1. Dominio (si hay lógica nueva)

Agregar método a la entidad en `Domain/{Entidad}/{Entidad}.cs`:

```csharp
public Result<int> MiNuevoComportamiento(parametros)
{
    // validar
    // mutar estado
    return Result.Success(valor);
}
```

Agregar errores en `Domain/{Entidad}/{Entidad}Errors.cs`.

### 2. Application — Command o Query

Crear carpeta `Application/{Entidad}/Commands/MiAccion/`:
- `MiAccionCommand.cs` — record con los datos de entrada
- `MiAccionCommandHandler.cs` — inyecta repositorios e interfaces de clientes HTTP
- `MiAccionResultDto.cs` (si devuelve datos estructurados)

### 3. Controller

Agregar endpoint en `Adapter/Controllers/{Entidad}Controller.cs`:

```csharp
[HttpPost("{id:guid}/mi-accion")]
public async Task<IActionResult> MiAccion(Guid id, [FromBody] MiAccionRequest request, CancellationToken ct)
{
    var result = await _sender.Send(new MiAccionCommand(id, request.Campo), ct);
    return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
}
public record MiAccionRequest(string Campo);
```

### 4. Migración (si hay cambios de BD)

```bash
dotnet ef migrations add NombreDeMigracion
dotnet ef database update
```

> **Atención**: Si la migración agrega una columna NOT NULL a una tabla con datos existentes,
> añadir un `migrationBuilder.Sql("UPDATE ...")` antes de crear índices únicos.

### 5. Front-end operador (`UMBRAL_Front-end`)

- Tipos en `src/types/`
- Servicios HTTP en `src/services/`
- Componentes en `src/components/Sessions/` o la carpeta correspondiente

### 6. Front-end participante (`UMBRAL_Front-end_Participantes`)

- Diseño **mobile-first**, tema oscuro (`#0f172a` fondo, `#1e293b` cards, `#6366f1` acento)
- Navegación por **máquina de estados** en `App.tsx` (sin react-router)
- Pantallas en `src/screens/`
- Solo llaman a **SessionService** (nunca a TeamService/StageService directamente — seguridad)

### 7. Tests unitarios

Archivo en `UMBRAL_Back-end.Tests/Domain/` o `Application/{Servicio}/`.

Estructura:

```csharp
public class MiFeatureTests
{
    [Fact]
    public void Handle_WhenCondicion_DebeResultado()
    {
        // Arrange
        var mock = new Mock<IDependency>();
        // Act
        var result = ...;
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
    }
}
```

Para mockear SignalR:

```csharp
var hubMock     = new Mock<IHubContext<SessionHub>>();
var clientsMock = new Mock<IHubClients>();
var proxyMock   = new Mock<IClientProxy>();
clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
```

---

## Convenciones importantes

| Qué | Regla |
|---|---|
| Idioma del código | Inglés (nombres, comentarios, UI strings) |
| Mensajes de auditoría | Español |
| Puntaje | Puede ser negativo (sin piso) |
| CORS | Siempre agregar 5173 y 5174 en cada servicio nuevo |
| Eventos de auditoría | Escribir `SessionEvent` en los handlers de SessionService tras cada acción del operador |
| Índices únicos en migración | Hacer UPDATE previo si la tabla tiene datos existentes |

---

## Historial de HUs implementadas

| HU | Descripción | Servicios tocados |
|---|---|---|
| HU-1 a HU-8 | Gestión de misiones, etapas, pistas, sesiones | MissionService, StageService, ClueService, SessionService |
| HU-9 | Dashboard operacional | SessionService |
| HU-10 | Monitoreo en tiempo real (SignalR) | SessionService, TeamService |
| HU-11 a HU-13 | Estados de sesión, inicio, liberar pistas | SessionService, TeamService |
| HU-14 | Auto-liberación de pistas por timer | SessionService (BackgroundService) |
| HU-15 | Penalizar equipo | SessionService, TeamService |
| HU-16 | Forzar avance de etapa | SessionService, TeamService |
| HU-17 | Ingreso de participantes y formación de equipo | SessionService, TeamService, Front Participante |
| HU-18 | Responder trivia | SessionService, TeamService, StageService, Front Participante |

---

## Variables de entorno relevantes (appsettings.json)

```json
{
  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Database=...;Username=...;Password=..." },
  "TeamServiceUrl":    "http://localhost:5095",
  "ClueServiceUrl":    "http://localhost:5094",
  "StageServiceUrl":   "http://localhost:5093",
  "MissionServiceUrl": "http://localhost:5091"
}
```
