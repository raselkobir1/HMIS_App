# HMIS web UI: Angular production build served by nginx, which also proxies /api to the API container.
# Build context: repository root.

FROM node:24-alpine AS build
WORKDIR /app
COPY src/Frontend/hospital-ui/package.json src/Frontend/hospital-ui/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/Frontend/hospital-ui/ .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY --from=build /app/dist/hospital-ui/browser /usr/share/nginx/html
COPY docker/ui/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
