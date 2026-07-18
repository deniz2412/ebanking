# Build the Angular SPA and serve it with nginx (which also proxies /api to the gateway).
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
# nginx:alpine renders templates (envsubst) and runs /docker-entrypoint.d/*.sh on startup.
ENV GATEWAY_URL=http://gateway:8080
COPY default.conf.template /etc/nginx/templates/default.conf.template
COPY docker-entrypoint.sh /docker-entrypoint.d/40-ebanking-config.sh
# Angular's application builder emits to dist/web/browser.
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/40-ebanking-config.sh
EXPOSE 80
