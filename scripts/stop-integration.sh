#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🛑 Stopping MTG API Integration Environment${NC}"
echo "==============================================="

# Stop and remove containers
echo -e "${YELLOW}🔄 Stopping services...${NC}"
docker compose -f docker-compose.int.yml down

echo -e "${YELLOW}🧹 Cleaning up containers and networks...${NC}"
docker compose -f docker-compose.int.yml down --remove-orphans

# Optional: Remove volumes (uncomment if you want to reset data)
read -p "Do you want to remove data volumes? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}🗑️  Removing data volumes...${NC}"
    docker compose -f docker-compose.int.yml down -v
    echo -e "${GREEN}✅ Data volumes removed${NC}"
else
    echo -e "${BLUE}ℹ️  Data volumes preserved${NC}"
fi

echo -e "${GREEN}✅ Integration environment stopped successfully${NC}"
echo ""
echo -e "${BLUE}To start again:${NC}"
echo "  ./scripts/start-integration.sh"