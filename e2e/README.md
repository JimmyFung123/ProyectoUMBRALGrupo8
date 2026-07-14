# Pruebas E2E — UMBRAL (Playwright)

Pruebas de punta a punta que manejan **navegadores reales** contra el stack
completo: el front del **operador** (Keycloak + gateway + microservicios) y el
front de **participantes** (anónimos, por código de invitación), incluyendo el
tiempo real por SignalR.

## Por qué Playwright
- **Multi-actor**: un mismo test puede correr el operador y varios participantes
  en paralelo con `browser.newContext()` — que es exactamente el flujo de UMBRAL
  (el operador arranca la sesión, los equipos juegan, el ranking se actualiza).
- **Dos frontends** en orígenes distintos (`:5173` operador, `:5174` participante):
  Playwright navega cross-origin sin fricción.
- **Auth**: se loguea al operador una sola vez (Keycloak) y se reusa la sesión.

## Requisitos
1. **Stack levantado** (Docker deploy):
   ```bash
   # desde la raíz del repo
   docker compose -f docker-compose.yml -f docker-compose.deploy.yml up -d
   ```
   Las imágenes de front vienen horneadas con URLs `localhost:*`, alcanzables
   desde el navegador que maneja Playwright.
2. **Dependencias + navegador** (una vez):
   ```bash
   cd e2e
   npm install
   npm run install:browsers
   ```

## Correr
```bash
cd e2e
npm test              # headless
npm run test:headed   # viendo el navegador
npm run test:ui       # modo UI interactivo
npm run report        # abre el último reporte HTML
```

## Autenticación (dos identidades)
El front operador **separa la UI por rol de forma excluyente** (ver
`UMBRAL_Front-end/src/App.jsx`): un usuario `admin` ve Misiones/Estadísticas/…
pero **no** Sesiones, y un `operator` ve **solo** Sesiones. Por eso el flujo
completo usa dos identidades:

- **Administrador** (`admin@umbral.local`, seed del realm) → crea/activa misiones.
- **Operador** (`operador.e2e@umbral.local`) → gestiona la sesión en vivo. El
  realm no trae ningún usuario `operator`, así que `global-setup.ts` lo
  **aprovisiona por la Admin REST API de Keycloak** (idempotente, contraseña
  permanente, sin acciones pendientes) antes de loguearlo.

`global-setup.ts` loguea a ambos una vez y guarda sus sesiones en
`playwright/.auth/admin.json` y `playwright/.auth/operator.json`; los tests las
reutilizan. Los participantes entran por código de invitación, sin auth.

Credenciales y URLs se sobrescriben por `.env` (ver `.env.example`).

## Estructura y roadmap
- `tests/smoke.spec.ts` — **Fase 0** ✅: operador autenticado + pantalla de
  ingreso del participante. Valida que la tubería E2E esté sana.
- `tests/flujo-completo.spec.ts` — **Fase 1** ✅: el "test estrella". Operador
  crea/arranca la sesión; 2 participantes entran (mismo equipo, RB-18 ≥2),
  responden trivia (**fallo → opción bloqueada + pista por reintento**;
  **acierto → avanza y suma**), y el **ranking en vivo del operador** se
  actualiza de "Etapa 2" a **"🏁 Finalizó"** al terminar la última etapa. Cubre
  los dos fixes recientes (commits `478755c` y `8e14bf4`).
  - La misión (misión + etapas + pistas + regla "Intentos fallidos" + activación)
    se **siembra por API** (`mission-fixture.ts`; controllers públicos) para que
    la precondición sea robusta y rápida. El resto del flujo va por UI real +
    SignalR. Un test que ejercite la creación de misión por la UI de
    administración es buen material para Fase 2.
- **Fase 2**: casos borde (equipo incompleto/RB-18, treasure hunt/QR,
  penalizaciones, broadcast del operador, creación de misión por UI admin).

## Tips para escribir tests
- `npm run codegen:operador` / `codegen:participante` graba interacciones y
  sugiere selectores contra el stack corriendo.
- Preferir selectores por rol/texto (`getByRole`, `getByText`, `getByPlaceholder`)
  sobre CSS frágil. Si un flujo necesita un selector estable, agregar un
  `data-testid` en el front es válido.
