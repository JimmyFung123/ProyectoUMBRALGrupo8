# UMBRAL — Despliegue self-host + CI/CD

Guía para correr **todo en contenedores** en tu PC y exponerlo gratis por
internet, con **auto-deploy** (push a `develop`) y **rollback automático** al
último commit estable si el health-check falla.

> Pensado para self-host (tu PC como servidor). Para tráfico real / 24-7 conviene
> un VPS o nube; ver opciones al final.

---

## 1. Arquitectura

| Capa | Qué | Cómo |
|------|-----|------|
| Infra | PostgreSQL (5 BDs), RabbitMQ, Keycloak (+BD), Mailpit | `docker-compose.yml` |
| Backend | 6 microservicios .NET (Dockerfile c/u) | `docker-compose.deploy.yml` |
| Fronts | Operador (estático) + Participante (estático + proxy nginx) | `docker-compose.deploy.yml` |
| Acceso público | Cloudflare Tunnel (HTTPS gratis) | servicio `cloudflared` (profile `tunnel`) |
| CI/CD | push a `develop` → deploy + rollback | `.github/workflows/deploy.yml` + `scripts/deploy.ps1` |

Todos los servicios comparten la red de Docker y se ven por nombre
(`postgres`, `rabbitmq`, `keycloak`, `missionservice`, …).

---

## 2. Requisitos

- **Docker Desktop** (engine en modo Linux).
- **.NET 10 SDK** + `dotnet-ef` global (`dotnet tool install --global dotnet-ef`) — las migraciones corren en el host.
- **git**.
- (Opcional) Una cuenta **Cloudflare** + un dominio para el túnel con nombre.

---

## 3. Probar el stack completo en local (sin túnel)

```powershell
cp .env.example .env        # los defaults ya sirven para local
docker compose -f docker-compose.yml -f docker-compose.deploy.yml up -d --build
# migraciones (una vez que Postgres esté arriba):
.\scripts\deploy.ps1        # build + migraciones + up + health-check
```

Quedan en:

| Servicio | URL |
|----------|-----|
| Front operador | http://localhost:5173 |
| Front participante | http://localhost:5174 |
| UserService (HU-23) | http://localhost:5096/swagger |
| Keycloak | http://localhost:18090 (admin/admin) |
| Mailpit | http://localhost:8025 |

> **Gotcha de login en local-Docker:** el navegador obtiene tokens con
> `issuer = http://localhost:18090/...`, pero los servicios (dentro de Docker)
> bajan el JWKS por la red interna. Ya está resuelto: el compose pone
> `KC_HOSTNAME` + `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true` en Keycloak y los
> servicios usan `Keycloak__MetadataAddress` (interno) con `Authority` (público).
> Si cambiás la URL pública de Keycloak, mantené `KEYCLOAK_AUTHORITY` en `.env`
> igual al `issuer` que ven los tokens.

Bajar todo: `docker compose -f docker-compose.yml -f docker-compose.deploy.yml down`

---

## 4. CI/CD — auto-deploy con GitHub Actions + self-hosted runner

### 4.1 Registrar el runner (requiere admin del repo)

En GitHub: **Settings → Actions → Runners → New self-hosted runner → Windows**.
Seguí los comandos que muestra (descarga + `config.cmd` con el token). Al
configurarlo, **aceptá las labels por defecto** (`self-hosted`, `Windows`) — el
workflow las usa. Instalalo como servicio para que arranque solo:

```powershell
# dentro de la carpeta del runner, tras config.cmd:
.\svc.cmd install
.\svc.cmd start
```

### 4.2 Configurar el `.env` en la PC del runner

El runner ejecuta `deploy.ps1`, que lee el `.env` **local** del repo (no se
commitea). Copiá `.env.example` a `.env` y completá secretos/URLs. Así no hace
falta meter secretos en GitHub.

### 4.3 Cómo funciona

1. Hacés push a `develop` → GitHub dispara `deploy.yml` en tu runner.
2. `checkout` trae el commit (con historial completo).
3. `deploy.ps1`: infra → migraciones → `build + up` → **health-check** de los 8 contenedores.
   - **Sano** → guarda el commit como *último estable* (en `~/.umbral-deploy/last-good-commit`).
   - **Falla** → `git checkout` al último estable + redeploy, y marca el run como fallido.

Probarlo a mano: `.\scripts\deploy.ps1` (o `-Ref <sha>`, o `-NoRollback` para depurar).

---

## 5. Exponerlo a internet — Cloudflare Tunnel (gratis + HTTPS)

1. **Cloudflare Zero Trust → Networks → Tunnels → Create a tunnel** (tipo *Cloudflared*).
2. Copiá el **token** del túnel → `.env`: `CLOUDFLARE_TUNNEL_TOKEN=...`.
3. Levantá el túnel:
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.deploy.yml --profile tunnel up -d
   ```
4. En el dashboard del túnel, agregá las **Public Hostnames** (un subdominio → un servicio interno):

   | Hostname público | Servicio (interno) |
   |------------------|--------------------|
   | `app.tudominio` | `http://front-operador:80` |
   | `play.tudominio` | `http://front-participante:80` |
   | `kc.tudominio` | `http://keycloak:8080` |
   | `user-api.tudominio` | `http://userservice:8080` |
   | `mission-api.tudominio` | `http://missionservice:8080` |
   | `session-api.tudominio` | `http://sessionservice:8080` |
   | `stage-api.tudominio` | `http://stageservice:8080` |
   | `clue-api.tudominio` | `http://clueservice:8080` |
   | `team-api.tudominio` | `http://teamservice:8080` |

5. Poné en `.env` las URLs públicas y reconstruí los fronts (Vite hornea las URLs en build):
   ```
   PUBLIC_KEYCLOAK_URL=https://kc.tudominio
   PUBLIC_USER_API_URL=https://user-api.tudominio/api
   PUBLIC_MISSION_API_URL=https://mission-api.tudominio/api
   PUBLIC_SESSION_API_URL=https://session-api.tudominio/api
   PUBLIC_SESSION_SIGNALR_URL=https://session-api.tudominio/hubs/session
   PUBLIC_STAGE_API_URL=https://stage-api.tudominio/api
   PUBLIC_CLUE_API_URL=https://clue-api.tudominio/api
   PUBLIC_TEAM_API_URL=https://team-api.tudominio/api
   CORS_OPERADOR_ORIGIN=https://app.tudominio
   CORS_PARTICIPANTE_ORIGIN=https://play.tudominio
   KEYCLOAK_AUTHORITY=https://kc.tudominio/realms/umbral
   KEYCLOAK_REQUIRE_HTTPS=true
   ```
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.deploy.yml build front-operador
   docker compose -f docker-compose.yml -f docker-compose.deploy.yml up -d
   ```
6. **Keycloak detrás del túnel:** además de `KC_HOSTNAME` (ya sale de `PUBLIC_KEYCLOAK_URL`),
   agregá en el servicio `keycloak` `KC_PROXY_HEADERS=xforwarded` para que respete el
   `https` que termina Cloudflare. Actualizá en el realm los `redirectUris`/`webOrigins`
   del cliente `umbral-frontend` con `https://app.tudominio/*`.

---

## 6. Rollback — qué pasa cuando algo sale mal

- `deploy.ps1` guarda el **último commit que pasó el health-check** fuera del repo
  (`~/.umbral-deploy/last-good-commit`), así sobrevive al `git clean` de checkout.
- Si un push deja algún contenedor sin responder, hace `git checkout --force` a ese
  commit y vuelve a levantar → **siempre quedás con la última versión estable corriendo**.
- El run de Actions queda en rojo (para que te enteres), aunque el servicio esté sano.

---

## 7. Limitaciones honestas

- **Self-host**: tu PC debe estar encendida y con internet. No es para producción seria.
- **Secretos**: cambiá `POSTGRES_PASSWORD`, `UMBRAL_BACKEND_SECRET` y el admin de
  Keycloak antes de exponerlo. El realm `umbral-backend` trae un secret de ejemplo.
- **Migraciones**: corren en el host (necesita .NET SDK + `dotnet-ef`). Un servidor sin
  SDK necesitaría *migration bundles*.
- **Front participante**: su build corre `tsc -b`; si hay errores de tipos, el build de
  esa imagen falla (no así el operador, que solo hace `vite build`).
- **Costos / escala**: si necesitás 24-7 y escala, pasá a un VPS (~5€/mes, mismo
  `docker compose up`) o a nube gestionada.
