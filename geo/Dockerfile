# Internal IP -> location / network lookup (DB-IP Lite databases). Build context: ./geo
FROM node:22-alpine
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --no-audit --no-fund
COPY server.mjs ./
# Build identity for GET /version (Coolify passes SOURCE_COMMIT); last, to keep the layers above cached.
ARG SOURCE_COMMIT=unknown
RUN echo "$SOURCE_COMMIT" > /app/SOURCE_COMMIT && date -u +%Y-%m-%dT%H:%M:%SZ > /app/BUILD_TIME
RUN mkdir -p /data && chown node:node /data
USER node
ENV PORT=3100 DATA_DIR=/data NODE_ENV=production
EXPOSE 3100
HEALTHCHECK --interval=30s --timeout=5s CMD node -e "fetch('http://127.0.0.1:3100/health').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.mjs"]
