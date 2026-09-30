# syntax=docker/dockerfile:1
# Builds the UI and serves it with nginx, which also proxies /api and /hubs to the API (nginx/).

FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Runs as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.29-alpine
COPY nginx/ /etc/nginx/conf.d/
COPY --from=build /app/dist /usr/share/nginx/html
