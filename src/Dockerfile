# Nuxt SPA served by nginx; nginx also proxies /api to the API container. Build context: ./src
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY . .
ARG NUXT_APP_BASE_URL=/
ENV NUXT_APP_BASE_URL=$NUXT_APP_BASE_URL
ARG NUXT_PUBLIC_DEMO_INVITE_CODE=
ENV NUXT_PUBLIC_DEMO_INVITE_CODE=$NUXT_PUBLIC_DEMO_INVITE_CODE
# Operator details for /legal/imprint, /legal/privacy and /legal/terms (address lines separated by "|").
ARG NUXT_PUBLIC_LEGAL_NAME=
ENV NUXT_PUBLIC_LEGAL_NAME=$NUXT_PUBLIC_LEGAL_NAME
ARG NUXT_PUBLIC_LEGAL_ADDRESS=
ENV NUXT_PUBLIC_LEGAL_ADDRESS=$NUXT_PUBLIC_LEGAL_ADDRESS
ARG NUXT_PUBLIC_LEGAL_EMAIL=
ENV NUXT_PUBLIC_LEGAL_EMAIL=$NUXT_PUBLIC_LEGAL_EMAIL
ARG NUXT_PUBLIC_LEGAL_VAT_ID=
ENV NUXT_PUBLIC_LEGAL_VAT_ID=$NUXT_PUBLIC_LEGAL_VAT_ID
ARG NUXT_PUBLIC_LEGAL_COUNTRY=Austria
ENV NUXT_PUBLIC_LEGAL_COUNTRY=$NUXT_PUBLIC_LEGAL_COUNTRY
RUN npm run generate
# Build identity, served as /version.json (Coolify passes SOURCE_COMMIT).
ARG SOURCE_COMMIT=unknown
RUN printf '{"commit":"%s","builtAt":"%s"}\n' "$SOURCE_COMMIT" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > .output/public/version.json

FROM nginx:1.29-alpine
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/.output/public /usr/share/nginx/html
# Where to reach the API (compose service name by default)
ENV API_UPSTREAM=http://api:8080
# Shared host whose landing/pricing/legal pages may be indexed (empty: nothing is indexed). See nginx.conf.template.
ENV PUBLIC_INDEX_HOST=
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s CMD wget -qO- http://127.0.0.1/healthz || exit 1
