# syntax=docker/dockerfile:1
# Front participante (UMBRAL_Front-end_Participantes). Build estático con Vite,
# servido por nginx. A diferencia del operador, NO usa URLs absolutas: llama a
# rutas relativas (/api, /team-api, /hubs) y nginx hace de proxy a los servicios
# (mismo origen ⇒ sin CORS, igual que el proxy de Vite en dev).

# node:22 (glibc) — Vite/rolldown necesita Node >=22 y su binding nativo gnu.
# Copiamos solo package.json (NO el lock) para evitar el bug de npm con
# dependencias opcionales nativas resueltas en otro SO (npm/cli#4828).
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY package.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
