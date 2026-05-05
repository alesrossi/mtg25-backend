#!/bin/bash
# Manual deploy script for the Hetzner single-server setup.
# CI uses appleboy/ssh-action directly; this script is for local/manual deploys.
#
# Usage:
#   HETZNER_HOST=<ip> HETZNER_USER=root ./scripts/deploy-hetzner.sh [image-tag]
#
# Defaults to int-latest if no image tag is passed.

set -euo pipefail

HOST="${HETZNER_HOST:?Set HETZNER_HOST}"
USER="${HETZNER_USER:-root}"
DEPLOY_PATH="${HETZNER_DEPLOY_PATH:-/opt/mtg}"
IMAGE_TAG="${1:-int-latest}"
SSH_KEY="${HETZNER_SSH_KEY_PATH:-$HOME/.ssh/hetzner-k3s}"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${BLUE}Deploying mtg-api:${IMAGE_TAG} → ${USER}@${HOST}:${DEPLOY_PATH}${NC}"

ssh -i "$SSH_KEY" -o StrictHostKeyChecking=no "${USER}@${HOST}" bash <<REMOTE
set -euo pipefail
cd "${DEPLOY_PATH}"

echo "Pulling latest code..."
git pull origin int

echo "Pulling updated images..."
IMAGE_TAG="${IMAGE_TAG}" docker compose -f docker-compose.prod.yml pull

echo "Restarting services..."
IMAGE_TAG="${IMAGE_TAG}" docker compose -f docker-compose.prod.yml up -d --remove-orphans

echo "Waiting for API health check..."
attempt=1
while [ \$attempt -le 24 ]; do
    if curl -sf http://localhost:8080/api/health > /dev/null 2>&1; then
        echo "API is healthy."
        break
    fi
    echo "  Attempt \$attempt/24 — not ready yet..."
    sleep 5
    (( attempt++ ))
done

if [ \$attempt -gt 24 ]; then
    echo "API failed to become healthy after 2 minutes."
    docker compose -f docker-compose.prod.yml logs --tail=30 api-int
    exit 1
fi
REMOTE

echo -e "${GREEN}Deploy complete. Health check: https://api.surveyl.top/api/health${NC}"
