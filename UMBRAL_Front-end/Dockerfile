# syntax=docker/dockerfile:1
# Front operador (UMBRAL_Front-end). Build estático con Vite → servido por nginx.
# Las VITE_* se inyectan como ARG en build time: Vite las "hornea" en el bundle,
# así que para apuntar a las URLs públicas hay que (re)construir la imagen.

# node:22 (glibc) — Vite/rolldown necesita Node >=22 y su binding nativo gnu.
# Copiamos solo package.json (NO el lock) para evitar el bug de npm con
# dependencias opcionales nativas resueltas en otro SO (npm/cli#4828).
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY package.json ./
RUN npm install
COPY . .

ARG VITE_KEYCLOAK_URL=http://localhost:18090
ARG VITE_KEYCLOAK_REALM=umbral
ARG VITE_KEYCLOAK_CLIENT_ID=umbral-frontend
ARG VITE_API_URL=http://localhost:5091/api
ARG VITE_SESSION_API_URL=http://localhost:5092/api
ARG VITE_SESSION_SIGNALR_URL=http://localhost:5092/hubs/session
ARG VITE_STAGE_API_URL=http://localhost:5093/api
ARG VITE_CLUE_API_URL=http://localhost:5094/api
ARG VITE_TEAM_API_URL=http://localhost:5095/api
ARG VITE_USER_API_URL=http://localhost:5096/api
ENV VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL
ENV VITE_KEYCLOAK_REALM=$VITE_KEYCLOAK_REALM
ENV VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_SESSION_API_URL=$VITE_SESSION_API_URL
ENV VITE_SESSION_SIGNALR_URL=$VITE_SESSION_SIGNALR_URL
ENV VITE_STAGE_API_URL=$VITE_STAGE_API_URL
ENV VITE_CLUE_API_URL=$VITE_CLUE_API_URL
ENV VITE_TEAM_API_URL=$VITE_TEAM_API_URL
ENV VITE_USER_API_URL=$VITE_USER_API_URL
RUN npm run build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
