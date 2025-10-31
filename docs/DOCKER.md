# Docker Support

This document describes how to build and run the Nuxt CV application using Docker.

## Prerequisites

- Docker 20.10 or higher
- Docker Compose v2 (optional, for docker-compose.yml)

## Quick Start

### Using Docker Compose (Recommended)

```bash
# Build and start the container
docker compose up -d

# View logs
docker compose logs -f

# Stop the container
docker compose down
```

The application will be available at http://localhost:3000

### Using Docker CLI

```bash
# Build the image
docker build -t nuxt-cv .

# Run the container
docker run -d -p 3000:3000 --name nuxt-cv nuxt-cv

# View logs
docker logs -f nuxt-cv

# Stop the container
docker stop nuxt-cv
docker rm nuxt-cv
```

## Configuration

### Environment Variables

You can configure the application using environment variables. The following variables are supported:

- `NUXT_APP_BASE_URL` - Base URL for the application (default: `/`)
- `AUTH_ENABLED` - Enable/disable authentication (default: `false`)
- `AUTH_TYPE` - Authentication type: `token` or `basic`
- `AUTH_TOKEN` - Token for token-based authentication
- `AUTH_USERNAME` - Username for basic authentication
- `AUTH_PASSWORD` - Password for basic authentication
- `NODE_ENV` - Node environment (default: `production`)

### Using Environment Variables with Docker

**With docker-compose.yml:**

Edit the `docker-compose.yml` file to add or modify environment variables:

```yaml
environment:
  - NUXT_APP_BASE_URL=/
  - AUTH_ENABLED=true
  - AUTH_TYPE=token
  - AUTH_TOKEN=your-secret-token
```

**With Docker CLI:**

```bash
docker run -d \
  -p 3000:3000 \
  -e AUTH_ENABLED=true \
  -e AUTH_TYPE=token \
  -e AUTH_TOKEN=your-secret-token \
  --name nuxt-cv \
  nuxt-cv
```

**Using an .env file:**

Create a `.env` file:

```env
NUXT_APP_BASE_URL=/
AUTH_ENABLED=true
AUTH_TYPE=token
AUTH_TOKEN=your-secret-token
```

Then use it with docker-compose:

```bash
docker compose --env-file .env up -d
```

Or with Docker CLI:

```bash
docker run -d \
  -p 3000:3000 \
  --env-file .env \
  --name nuxt-cv \
  nuxt-cv
```

## Docker Image Details

### Multi-stage Build

The Dockerfile uses a multi-stage build process:

1. **Builder stage**: Installs dependencies and builds the application
2. **Runner stage**: Creates a minimal production image with only the built application

### Image Size

The final image is based on `node:20-alpine` for minimal size (~100MB compressed).

### Security

- Runs as a non-root user (`nuxtjs`)
- Only includes production dependencies
- Uses official Node.js Alpine image

## Health Check

The docker-compose.yml includes a health check that tests the application's availability:

```yaml
healthcheck:
  test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:3000"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```

You can check the health status:

```bash
docker inspect --format='{{json .State.Health}}' nuxt-cv
```

## Production Deployment

### Using Docker Registry

1. Build and tag the image:

```bash
docker build -t your-registry.com/nuxt-cv:latest .
```

2. Push to registry:

```bash
docker push your-registry.com/nuxt-cv:latest
```

3. Pull and run on production server:

```bash
docker pull your-registry.com/nuxt-cv:latest
docker run -d -p 3000:3000 your-registry.com/nuxt-cv:latest
```

### Using Docker Swarm

```bash
docker service create \
  --name nuxt-cv \
  --publish 3000:3000 \
  --replicas 3 \
  your-registry.com/nuxt-cv:latest
```

### Using Kubernetes

Create a deployment:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nuxt-cv
spec:
  replicas: 3
  selector:
    matchLabels:
      app: nuxt-cv
  template:
    metadata:
      labels:
        app: nuxt-cv
    spec:
      containers:
      - name: nuxt-cv
        image: your-registry.com/nuxt-cv:latest
        ports:
        - containerPort: 3000
        env:
        - name: NODE_ENV
          value: "production"
---
apiVersion: v1
kind: Service
metadata:
  name: nuxt-cv
spec:
  selector:
    app: nuxt-cv
  ports:
  - port: 80
    targetPort: 3000
  type: LoadBalancer
```

## Development

For development with certificate issues (e.g., corporate proxies), use the development Dockerfile:

```bash
# Build with development Dockerfile
docker build -f Dockerfile.dev -t nuxt-cv:dev .

# Run
docker run -d -p 3000:3000 --name nuxt-cv-dev nuxt-cv:dev
```

**Note**: `Dockerfile.dev` disables SSL certificate verification for npm install. This is **NOT recommended for production**.

## Troubleshooting

### Container won't start

Check the logs:

```bash
docker logs nuxt-cv
```

### Port already in use

Change the port mapping:

```bash
docker run -d -p 8080:3000 --name nuxt-cv nuxt-cv
```

Or in docker-compose.yml:

```yaml
ports:
  - "8080:3000"
```

### Build fails with certificate errors

This can happen in environments with corporate proxies or certificate issues. Use the development Dockerfile:

```bash
docker build -f Dockerfile.dev -t nuxt-cv .
```

### Permission denied errors

The container runs as a non-root user. If you need to mount volumes, ensure proper permissions:

```bash
docker run -d \
  -p 3000:3000 \
  -v ./data:/app/data \
  --user $(id -u):$(id -g) \
  --name nuxt-cv \
  nuxt-cv
```

## CI/CD Integration

See the `.github/workflows/` directory for examples of using Docker in CI/CD pipelines.

### GitHub Actions Example

```yaml
- name: Build Docker image
  run: docker build -t nuxt-cv:${{ github.sha }} .

- name: Test Docker image
  run: |
    docker run -d --name test-container -p 3000:3000 nuxt-cv:${{ github.sha }}
    sleep 5
    curl http://localhost:3000
    docker stop test-container
```

## Best Practices

1. **Use specific tags**: Instead of `latest`, use version tags like `nuxt-cv:1.0.0`
2. **Set resource limits**: Use `--memory` and `--cpus` flags to limit resource usage
3. **Use secrets management**: Don't include secrets in the image or docker-compose.yml
4. **Regular updates**: Keep the base image updated for security patches
5. **Monitor logs**: Use log aggregation tools to collect and analyze logs
6. **Health checks**: Always configure health checks for production deployments

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Nuxt Deployment Guide](https://nuxt.com/docs/getting-started/deployment)
