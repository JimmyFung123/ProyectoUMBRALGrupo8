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

## Autenticación
Solo el **operador** necesita login (Keycloak, usuario seed `admin@umbral.local`).
`global-setup.ts` lo loguea una vez y guarda la sesión en
`playwright/.auth/operator.json`; cada test del operador la reutiliza. Los
participantes entran por código de invitación, sin auth.

Credenciales y URLs se sobrescriben por `.env` (ver `.env.example`).

## Estructura y roadmap
- `tests/smoke.spec.ts` — **Fase 0**: operador autenticado + pantalla de ingreso
  del participante. Valida que la tubería E2E esté sana.
- **Fase 1** (siguiente): el "test estrella" — operador crea misión + sesión y la
  arranca; 2 participantes entran, responden trivia (fallo → pista por reintento;
  acierto → avanza), y el ranking en vivo se actualiza hasta mostrar "Finalizó".
- **Fase 2**: casos borde (equipo incompleto/RB-18, treasure hunt/QR,
  penalizaciones, broadcast del operador).

## Tips para escribir tests
- `npm run codegen:operador` / `codegen:participante` graba interacciones y
  sugiere selectores contra el stack corriendo.
- Preferir selectores por rol/texto (`getByRole`, `getByText`, `getByPlaceholder`)
  sobre CSS frágil. Si un flujo necesita un selector estable, agregar un
  `data-testid` en el front es válido.
