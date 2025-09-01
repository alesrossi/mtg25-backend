#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting MTG API Integration Environment (Local Build)${NC}"
echo "=================================================="

export IMAGE_TAG=${IMAGE_TAG:-"local"}

echo -e "${BLUE}Configuration:${NC}"
echo "  Image Tag: $IMAGE_TAG"
echo "  Full Image: mtg-api:$IMAGE_TAG"
echo ""

# Stop existing containers
echo -e "${YELLOW}🛑 Stopping existing integration containers...${NC}"
docker compose -f docker-compose.int.yml down --remove-orphans

# Clean up old containers and networks
echo -e "${YELLOW}🧹 Cleaning up old resources...${NC}"
docker system prune -f --filter "label=com.docker.compose.project=mtg-int" || true

# Build local image
echo -e "${YELLOW}🔨 Building local image: mtg-api:$IMAGE_TAG${NC}"
if ! docker build -t mtg-api:$IMAGE_TAG .; then
    echo -e "${RED}❌ Failed to build local image${NC}"
    exit 1
fi

# Create temporary override file for local image
cat > docker-compose.local-override.yml << EOF
version: '3.8'
services:
  api-int:
    image: mtg-api:local
EOF

# Start integration environment
echo -e "${YELLOW}🔧 Starting integration services...${NC}"
docker compose -f docker-compose.int.yml -f docker-compose.local-override.yml up -d

# Cleanup override file
rm -f docker-compose.local-override.yml

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
echo "  🏥 API Health:     http://localhost:8086/api/health"
echo "  📚 API Docs:       http://localhost:8086/swagger"
echo "  🗄️  Database Admin: http://localhost:8084"
echo "  🔄 Redis Port:     localhost:6380"
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
echo "  Password: supersecretlongpassword"
echo "  Databases: main_int, identity_int"