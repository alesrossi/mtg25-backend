#!/bin/bash
set -e

# Load .env if present to populate environment variables for docker compose.
if [ -f ".env" ]; then
    set -a
    . ./.env
    set +a
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting MTG API Integration Environment${NC}"
echo "=================================================="

# Validate required environment variables
if [ -z "$INT_POSTGRES_PASSWORD" ]; then
    echo -e "${RED}❌ Error: INT_POSTGRES_PASSWORD environment variable is required${NC}"
    echo "Export it with: export INT_POSTGRES_PASSWORD=yourpassword"
    exit 1
fi
if [ -z "$INT_JWT_SECRET" ]; then
    echo -e "${RED}❌ Error: INT_JWT_SECRET environment variable is required${NC}"
    echo "Export it with: export INT_JWT_SECRET=yourjwtsecret"
    exit 1
fi
# HMAC-SHA256 requires key size > 256 bits (33+ chars)
if [ "$(printf '%s' "$INT_JWT_SECRET" | wc -c)" -lt 33 ]; then
    echo -e "${RED}❌ Error: INT_JWT_SECRET must be at least 33 characters (>256 bits for HMAC-SHA256)${NC}"
    exit 1
fi

export IMAGE_TAG=${IMAGE_TAG:-"int-latest"}
export FRONTEND_REGISTRY=${FRONTEND_REGISTRY:-"alesrossi"}
export FRONTEND_TAG=${FRONTEND_TAG:-"latest"}

echo -e "${BLUE}Configuration:${NC}"
echo "  Docker Username: $DOCKER_USERNAME"
echo "  Image Tag: $IMAGE_TAG"
echo "  Full Image: $DOCKER_USERNAME/mtg-api:$IMAGE_TAG"
echo "  Frontend Image: $FRONTEND_REGISTRY/mtgfe-frontend:$FRONTEND_TAG"
echo "  TLS Proxy: https://localhost:8443"
echo ""

# Stop existing containers
echo -e "${YELLOW}🛑 Stopping existing integration containers...${NC}"
docker compose -f docker-compose.int.yml down --remove-orphans

# Clean up old containers and networks
echo -e "${YELLOW}🧹 Cleaning up old resources...${NC}"
docker system prune -f --filter "label=com.docker.compose.project=mtg-int" || true

# Pull latest image
echo -e "${YELLOW}📥 Pulling API image: $DOCKER_USERNAME/mtg-api:$IMAGE_TAG${NC}"
if ! docker pull --platform linux/amd64 $DOCKER_USERNAME/mtg-api:$IMAGE_TAG; then
    echo -e "${RED}❌ Failed to pull image. Make sure:${NC}"
    echo "  1. The image exists on DockerHub"
    echo "  2. Your DockerHub username is correct"
    echo "  3. The image tag exists"
    exit 1
fi

echo -e "${YELLOW}📥 Pulling Frontend image: $FRONTEND_REGISTRY/mtgfe-frontend:$FRONTEND_TAG${NC}"
if ! docker pull --platform linux/amd64 $FRONTEND_REGISTRY/mtgfe-frontend:$FRONTEND_TAG; then
    echo -e "${RED}❌ Failed to pull frontend image. Make sure:${NC}"
    echo "  1. The image exists on DockerHub"
    echo "  2. The registry/tag are correct"
    exit 1
fi

# Start integration environment
echo -e "${YELLOW}🔧 Starting integration services...${NC}"
docker compose -f docker-compose.int.yml up -d

echo -e "${YELLOW}⏳ Waiting for services to be ready...${NC}"

# Function to check if a service is healthy
check_service_health() {
    local service_name=$1
    local max_attempts=$2
    local attempt=1

    while [ $attempt -le $max_attempts ]; do
        if docker compose -f docker-compose.int.yml ps $service_name | grep -q "healthy\|Up"; then
            return 0
        fi
        echo -e "   Attempt $attempt/$max_attempts - $service_name not ready yet..."
        sleep 5
        ((attempt++))
    done
    return 1
}

check_http_endpoint() {
    local url=$1
    local max_attempts=$2
    local attempt=1

    while [ $attempt -le $max_attempts ]; do
        if curl -sf "$url" > /dev/null 2>&1; then
            return 0
        fi
        echo -e "   Attempt $attempt/$max_attempts - Endpoint $url not ready yet..."
        sleep 5
        ((attempt++))
    done
    return 1
}

# Check PostgreSQL
echo -e "${BLUE}🗄️  Checking PostgreSQL...${NC}"
if check_service_health "postgres-int" 24; then
    echo -e "${GREEN}✅ PostgreSQL is ready${NC}"
else
    echo -e "${RED}❌ PostgreSQL failed to start${NC}"
    docker compose -f docker-compose.int.yml logs postgres-int
    exit 1
fi

# Check Redis
echo -e "${BLUE}🔄 Checking Redis...${NC}"
if check_service_health "redis-int" 12; then
    echo -e "${GREEN}✅ Redis is ready${NC}"
else
    echo -e "${RED}❌ Redis failed to start${NC}"
    docker compose -f docker-compose.int.yml logs redis-int
    exit 1
fi

# Check Elasticsearch
echo -e "${BLUE}🔍 Checking Elasticsearch...${NC}"
if check_http_endpoint "http://localhost:9200/_cluster/health" 24; then
    echo -e "${GREEN}✅ Elasticsearch is ready${NC}"
else
    echo -e "${RED}❌ Elasticsearch failed to become ready${NC}"
    docker compose -f docker-compose.int.yml logs elasticsearch-int
    exit 1
fi

# Check Kibana
echo -e "${BLUE}📊 Checking Kibana...${NC}"
if check_http_endpoint "http://localhost:5601/api/status" 24; then
    echo -e "${GREEN}✅ Kibana is ready${NC}"
else
    echo -e "${RED}❌ Kibana failed to become ready${NC}"
    docker compose -f docker-compose.int.yml logs kibana-int
    exit 1
fi

# Check API with more detailed health check
echo -e "${BLUE}🌐 Checking API...${NC}"
api_attempts=30
api_attempt=1

while [ $api_attempt -le $api_attempts ]; do
    if curl -sf http://localhost:8086/api/health > /dev/null 2>&1; then
        break
    fi
    
    echo -e "   Attempt $api_attempt/$api_attempts - API not ready yet..."
    
    # Show API logs if it's taking too long
    if [ $api_attempt -eq 15 ]; then
        echo -e "${YELLOW}🔍 API is taking longer than expected. Recent logs:${NC}"
        docker compose -f docker-compose.int.yml logs --tail=10 api-int
    fi
    
    sleep 5
    ((api_attempt++))
done

if [ $api_attempt -le $api_attempts ]; then
    echo -e "${GREEN}✅ API is ready${NC}"
else
    echo -e "${RED}❌ API failed to start properly${NC}"
    echo -e "${YELLOW}Recent API logs:${NC}"
    docker compose -f docker-compose.int.yml logs --tail=20 api-int
    exit 1
fi

# Test API health endpoint
echo -e "${BLUE}🔍 Testing API health endpoint...${NC}"
health_response=$(curl -s http://localhost:8086/api/health)
echo -e "${GREEN}Health Response: $health_response${NC}"

echo ""
echo -e "${GREEN}🎉 Integration environment is ready!${NC}"
echo "=================================================="
echo -e "${BLUE}Services:${NC}"
echo "  🌐 API:            http://localhost:8086"
echo "  🔐 API (TLS):      https://localhost:8443"
echo "  🏥 API Health:     http://localhost:8086/api/health"
echo "  📚 API Docs:       http://localhost:8086/scalar"
echo "  🗄️  Database Admin: http://localhost:8084"
echo "  🔄 Redis Port:     localhost:6380"
echo "  📦 Elasticsearch:  http://localhost:9200"
echo "  📊 Kibana:         http://localhost:5601"
echo ""
echo -e "${BLUE}Useful Commands:${NC}"
echo "  View logs:    docker compose -f docker-compose.int.yml logs -f"
echo "  Stop env:     ./scripts/stop-integration.sh"
echo "  Restart API:  docker compose -f docker-compose.int.yml restart api-int"
echo ""
echo -e "${YELLOW}Database Connection (for external tools):${NC}"
echo "  Host: localhost"
echo "  Port: 5434"
echo "  Username: root"
echo "  Password: (use INT_POSTGRES_PASSWORD)"
echo "  Databases: main_int, identity_int"
