# Internal PDF renderer (Chromium via Playwright). Build context: ./pdf
FROM mcr.microsoft.com/playwright:v1.55.1-noble
WORKDIR /app
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund
COPY server.mjs ./
# Build identity for GET /version (Coolify passes SOURCE_COMMIT); last, to keep the layers above cached.
ARG SOURCE_COMMIT=unknown
RUN echo "$SOURCE_COMMIT" > /app/SOURCE_COMMIT && date -u +%Y-%m-%dT%H:%M:%SZ > /app/BUILD_TIME
USER pwuser
ENV PORT=3000 NODE_ENV=production
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s CMD node -e "fetch('http://127.0.0.1:3000/health').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.mjs"]
