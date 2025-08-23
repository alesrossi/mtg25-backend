#!/bin/bash

# Colors for output
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}📋 Integration Environment Logs${NC}"
echo "================================="

if [ "$1" == "api" ]; then
    echo "Following API logs..."
    docker-compose -f docker-compose.int.yml logs -f api-int
elif [ "$1" == "db" ]; then
    echo "Following Database logs..."
    docker-compose -f docker-compose.int.yml logs -f postgres-int
elif [ "$1" == "redis" ]; then
    echo "Following Redis logs..."
    docker-compose -f docker-compose.int.yml logs -f redis-int
else
    echo "Following all service logs..."
    docker-compose -f docker-compose.int.yml logs -f
fi