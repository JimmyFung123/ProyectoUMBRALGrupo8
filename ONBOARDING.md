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
│   ├── UserService/           # Puerto 5096 — personal operativo (HU-23, fachada Keycloak)
│   ├── Shared/UMBRAL.Auth/    # Librería compartida con JwtBearer + helpers
│   ├── Shared/UMBRAL.Contracts/ # Eventos integration (MassTransit)
│   └── UMBRAL_Back-end.Tests/ # xUnit + Moq + FluentAssertions
├── UMBRAL_Front-end/          # Puerto 5173 — operador/administrador (React 19 + Vite, auth Keycloak)
└── UMBRAL_Front-end_Participantes/  # Puerto 5174 — participantes anónimos, mobile-first (React 19 + Vite)
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
| **Identidad (HU-23)** | **Keycloak 25 con realm `umbral` (PKCE + JWT)** |
| Tests | xUnit + Moq + FluentAssertions |
| Frontend operador | React 19 + TypeScript + Vite + **keycloak-js** (puerto 5173) |
| Frontend participante | React 19 + TypeScript + Vite, **sin auth** (puerto 5174) |

---

## Identidad y autenticación (HU-23)

UMBRAL usa **Keycloak 25** como proveedor de identidad. El realm `umbral` se
importa automáticamente al primer arranque desde
`scripts/keycloak/umbral-realm.json` — no hay que tocar la consola manualmente
salvo que se quiera modificar algo del realm.

### URLs y credenciales

| Qué | URL / valor |
|---|---|
| Admin console de Keycloak | http://localhost:18090 (`admin` / `admin`, realm `master`) |
| OIDC well-known del realm | http://localhost:18090/realms/umbral/.well-known/openid-configuration |
| Admin inicial de UMBRAL | `admin@umbral.local` / `Umbral2026!` (realm `umbral`) |
| Client SPA (operador) | `umbral-frontend` — público, PKCE S256 |
| Client backend (Admin API) | `umbral-backend` — confidencial, service account |

### Cómo se valida el token en cada servicio

Toda la lógica vive en `UMBRAL_Back-end/Shared/UMBRAL.Auth/`:

- `UmbralAuthExtensions.AddUmbralJwtAuth(config)` — registra JwtBearer y
  aplana `realm_access.roles` en claims `umbral_role` para que
  `[Authorize(Roles = "admin")]` funcione directo.
- `OperatorPrincipal.GetOperatorDisplayName()` extiende `ClaimsPrincipal` para
  obtener el nombre del operador (`name` → `preferred_username` → `email`).
- `OperatorPrincipal.IsAdmin()` / `IsOperator()` — atajos de rol.

Cada servicio configura esto en su `Program.cs` con dos líneas:

```csharp
builder.Services.AddUmbralJwtAuth(builder.Configuration);
// …
app.UseAuthentication();
app.UseAuthorization();
```

### Política de autorización por endpoint

| Tipo | Decorator | Quién accede |
|---|---|---|
| Endpoints internos del operador | `[Authorize]` (default class-level en `SessionsController`) | Admin u Operador autenticado |
| Endpoints de gestión de personal (UserService) | `[Authorize(Roles="admin")]` | Solo administradores |
| Endpoints de participantes | `[AllowAnonymous]` (override explícito) | Cualquiera con código de sesión |

Los participantes (front 5174) NO usan Keycloak. Acceden a los endpoints
marcados `[AllowAnonymous]` usando solo el PIN de la sesión, como antes.

### Flujo OIDC en el front operador

`UMBRAL_Front-end/src/auth/AuthProvider.tsx` envuelve la app. Al primer
render, llama `keycloak.init({ onLoad: 'login-required' })`, lo cual:

1. Si no hay sesión → redirige al login del realm Keycloak.
2. Tras autenticar, vuelve al app con el código → keycloak-js lo intercambia
   por tokens (access + refresh) que se guardan en memoria.
3. El helper `services/http.ts` adjunta `Authorization: Bearer <token>` en
   cada request al backend y refresca el token automáticamente cuando le
   quedan ≤30 s de vida.

### Cómo agregar usuarios operativos

Como administrador, entrá a la pestaña **👥 Personal** del operador (5173).
- ➕ Nuevo usuario → email único, nombre, apellido, password temporal, rol.
- 🔄 Cambiar rol → entre Administrador y Operador (con protección al último admin).
- 🚫 Deshabilitar / ✅ Habilitar → soft-delete que preserva el historial de auditoría.

Todos los cambios se reflejan en Keycloak en tiempo real. El próximo
`access_token` del usuario afectado ya trae los roles nuevos.

### Variables de entorno (opcional, defaults sirven en local)

`UMBRAL_Front-end/.env` (no obligatorio, solo si querés apuntar a otro host):

```bash
VITE_KEYCLOAK_URL=http://localhost:18090
VITE_KEYCLOAK_REALM=umbral
VITE_KEYCLOAK_CLIENT_ID=umbral-frontend
VITE_USER_API_URL=http://localhost:5096/api
```

---

## Cómo levantar el proyecto

### Atajo: scripts/start.ps1

```powershell
.\scripts\start.ps1   # arranca infra + servicios + fronts en ventanas separadas
.\scripts\stop.ps1    # cierra todo (incluye VBCSCompiler para evitar locks)
```

### Backend (cada servicio en su propia terminal)

```bash
cd UMBRAL_Back-end/SessionService && dotnet run
cd UMBRAL_Back-end/TeamService    && dotnet run
cd UMBRAL_Back-end/ClueService    && dotnet run
cd UMBRAL_Back-end/StageService   && dotnet run
cd UMBRAL_Back-end/MissionService && dotnet run
cd UMBRAL_Back-end/UserService    && dotnet run    # HU-23
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

## Patrones de diseño (GoF)

Además de los patrones arquitectónicos (hexagonal, CQRS, Result), el equipo aplicó
patrones de diseño GoF como parte del entregable. **Todos viven en `SessionService`**
(el orquestador, donde está la lógica de juego más rica).

| Patrón | Tipo | Dónde | Qué resuelve |
|---|---|---|---|
| **Strategy** | Comportamiento | `Application/Sessions/Scoring/` | El cálculo de puntaje cambia según la dificultad de la misión |
| **Template Method** | Comportamiento | `Application/Sessions/Commands/Evidence/` | Esqueleto común para procesar evidencias (trivia vs QR) con pasos que varían |
| **State** | Comportamiento | `Domain/Sessions/States/` | Qué transiciones son válidas según el estado de la sesión |
| **Chain of Responsibility** | Comportamiento | `Application/Sessions/Validation/` | Validar la evidencia paso a paso; cada validador decide si sigue la cadena |
| **Composite + Visitor** | Estructural / Comportamiento | `Application/Missions/Composite/` | Recorrer la jerarquía Misión → Etapas → Pistas de forma uniforme |
| **Facade** | Estructural | `Application/Sessions/Facade/` | Interfaz única para armar la vista de la etapa actual del participante (sesión + equipo + etapa) |
| **Proxy** | Estructural | `Infrastructure/ExternalClients/` | Cachear las llamadas a StageService sin que el consumidor se entere |

### Strategy — puntaje por dificultad
`IScoringStrategy` con `Easy/Medium/HardScoringStrategy` y `ScoringStrategyFactory`. El
handler `SubmitTriviaAnswerCommandHandler` pide la estrategia según la dificultad de la
misión y delega el cálculo del puntaje (positivo o penalización).

### Template Method — procesamiento de evidencias
`EvidenceHandlerBase` define el algoritmo fijo (validar → procesar → puntuar → auditar →
notificar por SignalR → armar DTO). Las subclases `SubmitTriviaAnswerCommandHandler` y
`ValidateQrCodeCommandHandler` solo rellenan los *hooks* que cambian.

### State — ciclo de vida de la sesión
`ISessionState` con `Pending / InProgress / Paused / Completed / CancelledState`. El
agregado `Session` delega `Start()/Pause()/Resume()/Finalize()/Cancel()` al estado actual,
que acepta o rechaza la transición. Evita los `if (status == …)` regados por el código.

### Chain of Responsibility — validación de evidencias
`EvidenceValidatorBase` encadena `SessionExistsValidator → SessionInProgressValidator →
StageExistsValidator`. Cada eslabón valida una regla y pasa al siguiente o corta la cadena.

### Composite + Visitor — estructura de una misión
`IMissionComponent` modela la jerarquía como árbol: `MissionComponent` (raíz) →
`StageComponent` → `ClueComponent` (hoja). `TotalScore()` agrega el puntaje recursivamente
y `MissionSummaryVisitor` recorre el árbol una sola vez para sacar el resumen. Caso de uso
real: **`GET /api/missions/{id}/structure`** (operador) devuelve el árbol completo con su
resumen (cantidad de etapas/pistas, puntaje total, etapas sin pistas).

> **Variante Composite seguro (LSP/ISP).** `Add`/`Remove` **no** están en la interfaz
> `IMissionComponent`: viven solo en `CompositeMissionComponent` (impl real) y como default
> que lanza en `MissionComponentBase` (para hojas). Así ningún miembro de la abstracción
> compartida lanza para algún subtipo y todo nodo es 100% sustituible.

```bash
# Demo (requiere token de operador)
curl -H "Authorization: Bearer <token>" \
     http://localhost:5092/api/missions/<missionId>/structure
```

### Facade — vista de la etapa actual del participante
Responder *qué etapa juega ahora* un equipo exige orquestar tres subsistemas (repositorio de
sesiones + TeamService + StageService) más reglas de auto-arranque, estados centinela
(`Waiting`/`Completed`) y ocultar la respuesta correcta. `IParticipantStageFacade` expone un
único método `GetCurrentStageAsync(sessionId, teamId)` que esconde toda esa orquestación. El
handler `GetParticipantStageQueryHandler` ahora **solo delega** en la fachada. Caso de uso
real: **`GET /api/sessions/{id}/participant-stage/{teamId}`** (participante).

> **SRP.** El *modelado/saneo* de la vista (ocultar `IsCorrect`, exponer coordenadas solo en
> `TreasureHunt`, centinelas `Waiting`/`Completed`) se extrajo a `ParticipantStageMapper`
> (puro, sin dependencias). La fachada queda solo con la orquestación y la decisión de estado;
> el mapper decide *cómo* se muestra. Una razón de cambio por clase.

```bash
# Demo (participante, sin token)
curl http://localhost:5092/api/sessions/<sessionId>/participant-stage/<teamId>
```

### Proxy — caché del cliente de StageService
El detalle de una etapa (`GetStageWithOptionsAsync`) se pide una y otra vez sobre las mismas
etapas: en cada respuesta de trivia/QR (Template Method), en cada poll del participante
(Facade), al re-sincronizar pistas y **N veces en el bucle del Composite**. Como ese detalle
es inmutable durante la partida, cachearlo es ganancia pura. `CachedStageServiceProxy`
implementa el mismo `IStageServiceClient` y envuelve al `StageServiceClient` real: memoiza
`GetStageWithOptionsAsync(stageId)` en un `IMemoryCache` (TTL absoluto de 30 s) y deja pasar
`GetStagesByMissionAsync` sin cachear. No cachea `null` (un fallo HTTP transitorio no debe
quedar pegado). Los consumidores siguen pidiendo `IStageServiceClient` y **no se enteran** de
la caché. En `Program.cs` se registra como decorador: el cliente concreto va por
`AddHttpClient<StageServiceClient>` y `IStageServiceClient` se resuelve como el proxy que lo
envuelve (con `AddMemoryCache()`, singleton → la caché se comparte entre peticiones).

```bash
# Demo: dos veces el mismo participant-stage dentro de 30 s. StageService recibe el detalle
# /api/stages/{id} UNA sola vez (2ª = cache hit); la lista ?missionId sí se golpea cada vez.
curl http://localhost:5092/api/sessions/<sessionId>/participant-stage/<teamId>
curl http://localhost:5092/api/sessions/<sessionId>/participant-stage/<teamId>
```

### Revisión SOLID (Composite, Facade, Proxy + SessionService)

Revisión de SRP/OCP/LSP/ISP/DIP sobre los tres patrones estructurales. Resumen:

- **DIP — bien.** Fachada, handlers y consumidores del proxy dependen de abstracciones
  (`ISessionRepository`, `ITeamServiceClient`, `IStageServiceClient`, `IParticipantStageFacade`);
  el proxy se inyecta como decorador y nadie ve la concreción cacheada.
- **LSP del Proxy — bien.** `CachedStageServiceProxy` no fortalece precondiciones ni debilita
  postcondiciones: mismas formas, **no cachea `null`** (fallo HTTP no pegajoso) y pasa
  `GetStagesByMissionAsync` directo. La única diferencia es staleness ≤30 s, que el contrato
  no prohíbe y es el propósito del proxy.

Refactors aplicados (commits `[Refactor]`):

| # | Principio | Cambio |
|---|---|---|
| C1 | LSP/ISP | `Add`/`Remove` fuera de `IMissionComponent` → Composite seguro (ver arriba) |
| F1 | SRP | `ParticipantStageMapper` extraído de `ParticipantStageFacade` (ver arriba) |

Hallazgos **señalados** (no cambiados por ser deliberados / limítrofes con sobre-ingeniería):

- **F2 (SRP/CQRS):** `GetCurrentStageAsync` ejecuta una escritura (`ForceAdvanceTeamAsync`,
  auto-arranque) dentro de un query. Recomendación: mover el auto-arranque al lado de comandos.
- **C2 (SRP):** `GetMissionStructureQueryHandler` valida + ensambla el árbol + proyecta a DTO
  (con `OfType<StageComponent>()`). Recomendación: extraer un `MissionStructureTreeBuilder`.

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
| HU-19 | Resolver etapa de Búsqueda del Tesoro | SessionService, TeamService, StageService, Front Participante |
| HU-20 | Visualización de pistas en la interfaz del juego | SessionService, Front Participante |
| HU-21 | Consultar ranking de la sesión (lectura optimizada + SignalR) | SessionService, TeamService, Front Operador, Front Participante |
| HU-22 | Consultar historial de auditoría de sesión | SessionService, Front Operador |
| HU-23 | Gestión integral de personal operativo (KeyCloak) | Infra (Docker), UMBRAL.Auth, UserService (5096), Front Operador |
| HU-26 | Auditoría y trazabilidad de acciones (log técnico de comandos CQRS + CSV) | SessionService (interceptor de inmutabilidad + endpoint `/audit-log`), Front Operador (pantalla `SessionCommandAuditScreen`) |
| HU-27 | Monitoreo de sincronización entre modelos de escritura y lectura | SessionService (aggregador `/api/sync-health` + reproject local), MissionService/StageService/ClueService/TeamService (endpoints `/api/internal/sync-health` + reproject por servicio), Front Operador (tab admin "🔄 Sincronización") |
| HU-28 | Feedback inmersivo e interactivo en vivo (toasts animados, vibración, confetti, mensaje del operador) | SessionService (eventos SignalR `StageCompleted` y `OperatorMessage`, comando `BroadcastOperatorMessage`), Front Operador (botón "Enviar mensaje" en `SessionControls` + `BroadcastMessageButton`), Front Participante (framer-motion + `NotificationStack` + `useGameEvents` + `vibrate`/`Confetti`) |

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
