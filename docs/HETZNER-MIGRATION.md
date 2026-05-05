# Hetzner Single-Server Migration

## Why

Switched from a Kubernetes (k3s, hetzner-k8s) cluster to a single 8 GB Hetzner server running
Docker Compose to cut running costs. The K8s cluster had a master node + autoscaling worker pool;
all that overhead is unnecessary for a low-traffic integration/production environment.

Elasticsearch and Kibana were dropped (saved ~1.5 GB RAM). API logs go to stdout/file only.
They can be re-enabled later; see the K8s section below for how to restore full observability.

---

## New Architecture

```
Internet → Caddy (TLS, Let's Encrypt) → services on mtg-int-network (Docker bridge)

surveyl.top / www.surveyl.top / app.surveyl.top  →  frontend-int:80
api.surveyl.top                                  →  api-int:8080
info.surveyl.top                                 →  info-site:80
privacy.surveyl.top                              →  privacy-site:80
adminer.surveyl.top  (basic auth)                →  adminer-int:8080
```

**Compose file:** `docker-compose.prod.yml`  
**Caddy config:** `config/Caddyfile`  
**appsettings:** mounted from `charts/mtg-int/files/appsettings.Integration.json` (no ES section active)

---

## One-Time Server Setup

Provision a Hetzner CX32 (8 GB RAM) with Ubuntu 24.04.

```bash
# On the server as root
apt update && apt install -y docker.io docker-compose-plugin git curl

# Clone the repo
mkdir -p /opt/mtg
git clone https://github.com/<org>/mtg25-backend.git /opt/mtg
cd /opt/mtg
git checkout int

# Create .env with secrets (never commit this file)
cat > /opt/mtg/.env <<'EOF'
INT_POSTGRES_PASSWORD=<strong-password>
INT_JWT_SECRET=<at-least-33-chars>
GOOGLE_CLIENT_ID=852016003016-a69888njse518ko1jvfjodv8m5gm9kln.apps.googleusercontent.com
DOCKER_USERNAME=alesrossi
IMAGE_TAG=int-latest
FRONTEND_REGISTRY=alesrossi
FRONTEND_TAG=latest
ADMINER_USER=admin
ADMINER_BASIC_AUTH_HASH=<caddy hash-password --plaintext yourpassword>
EOF

# Generate the Adminer Caddy hash on any machine with Docker:
# docker run --rm caddy:2-alpine caddy hash-password --plaintext yourpassword

# First deploy
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

### GitHub Secrets to add

| Secret | Value |
|---|---|
| `HETZNER_HOST` | Server IP or hostname |
| `HETZNER_USER` | `root` (or a deploy user) |
| `HETZNER_SSH_KEY` | Private SSH key that has access to the server |

The secrets `DOCKER_USERNAME`, `DOCKER_PASSWORD` already exist and are unchanged.

### DNS changes

Point all `surveyl.top` records to the new server IP (A record). Caddy will obtain
Let's Encrypt certificates automatically on first request — no cert manager needed.

```
surveyl.top         A   <new-server-ip>
www.surveyl.top     A   <new-server-ip>
app.surveyl.top     A   <new-server-ip>
api.surveyl.top     A   <new-server-ip>
info.surveyl.top    A   <new-server-ip>
privacy.surveyl.top A   <new-server-ip>
adminer.surveyl.top A   <new-server-ip>
```

---

## Daily Operations

```bash
# View logs
docker compose -f docker-compose.prod.yml logs -f api-int

# Restart a service
docker compose -f docker-compose.prod.yml restart api-int

# Manual deploy of a specific image tag
IMAGE_TAG=int-abc1234 docker compose -f docker-compose.prod.yml pull api-int
IMAGE_TAG=int-abc1234 docker compose -f docker-compose.prod.yml up -d api-int

# Run DB migrations manually (if not auto-applied on startup)
docker compose -f docker-compose.prod.yml exec api-int \
  dotnet ef database update --project Infrastructure --startup-project API

# Full teardown (keeps volumes)
docker compose -f docker-compose.prod.yml down

# Full teardown including data volumes (DESTRUCTIVE)
docker compose -f docker-compose.prod.yml down -v
```

---

## Switching Back to Kubernetes

Everything needed to restore the K8s setup is untouched in the repo. Steps:

### 1. Restore the GitHub Actions deploy job

In `.github/workflows/integration.yml`, replace the `deploy-hetzner` job with the original
`deploy-integration` job. The original is preserved in git history — use:

```bash
git show main:.github/workflows/integration.yml
```

Or copy the job from below:

<details>
<summary>Original deploy-integration job (click to expand)</summary>

```yaml
  deploy-integration:
    needs: build-and-push
    runs-on: ubuntu-latest
    name: Deploy to Hetzner Integration
    if: github.event_name == 'push' && github.ref == 'refs/heads/int'
    timeout-minutes: 10

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Install Helm
        uses: azure/setup-helm@v4
        with:
          version: v3.15.2

      - name: Configure kubeconfig
        env:
          KUBECONFIG_B64: ${{ secrets.KUBECONFIG_B64 }}
        run: |
          mkdir -p "$HOME/.kube"
          echo "$KUBECONFIG_B64" | base64 -d > "$HOME/.kube/config"
          chmod 600 "$HOME/.kube/config"

      - name: Deploy integration chart
        env:
          IMAGE_TAG: ${{ needs.build-and-push.outputs.image-tag }}
        run: |
          set -euo pipefail
          helm upgrade --install mtg-int ./charts/mtg-int \
            -n integration \
            --reuse-values \
            --set api.config.manageAppsettingsConfigMap=false \
            --set api.image.tag="$IMAGE_TAG" \
            --set kibana.enabled=true \
            --set nginx.certs.enabled=false \
            --debug

      - name: Install kubectl
        uses: azure/setup-kubectl@v4
        with:
          version: v1.30.3

      - name: Wait for rollout
        run: |
          kubectl -n integration rollout status deploy/api-int --timeout=3m
          kubectl -n integration rollout status deploy/frontend-int --timeout=3m
          kubectl -n integration rollout status deploy/kibana-int --timeout=3m

      - name: Health check
        run: curl -fsS https://api.surveyl.top/api/health

      - name: Debug on failure
        if: ${{ failure() }}
        run: |
          kubectl -n integration get pods -o wide
          kubectl -n integration get events --sort-by=.metadata.creationTimestamp | tail -n 100
          for pod in $(kubectl -n integration get pods --no-headers | awk '$2 != $3 || $4 != "Running" {print $1}'); do
            kubectl -n integration logs "$pod" --all-containers --tail=200 || true
          done
```
</details>

### 2. Re-add the GitHub Secret

| Secret | Value |
|---|---|
| `KUBECONFIG_B64` | `cat ~/hetzner-k3s-cluster/kubeconfig \| base64 -w0` |

The kubeconfig points to `https://91.107.233.123:6443` (master node).
Verify the cluster is still running before doing this:

```bash
KUBECONFIG=~/hetzner-k3s-cluster/kubeconfig kubectl get nodes
```

### 3. Check Helm release state

```bash
KUBECONFIG=~/hetzner-k3s-cluster/kubeconfig \
  helm status mtg-int -n integration
```

If the release is gone (e.g. cluster was reset), do a fresh install:

```bash
KUBECONFIG=~/hetzner-k3s-cluster/kubeconfig \
  helm upgrade --install mtg-int ./charts/mtg-int \
    -n integration \
    --create-namespace \
    -f charts/mtg-int/values.yaml \
    -f charts/mtg-int/values-secrets.yaml
```

`values-secrets.yaml` is not committed — you'll need to recreate it with:

```yaml
postgresql:
  auth:
    password: "<INT_POSTGRES_PASSWORD>"

api:
  secret:
    jwtSecret: "<INT_JWT_SECRET>"
    googleClientId: "852016003016-a69888njse518ko1jvfjodv8m5gm9kln.apps.googleusercontent.com"

frontend:
  secret:
    expoPublicEnv: "<value>"
    expoPublicAppName: "<value>"
    expoPublicApiUrl: "https://api.surveyl.top"
```

### 4. Point DNS back to the K8s cluster

```
surveyl.top         A   167.235.109.6
www.surveyl.top     A   167.235.109.6
app.surveyl.top     A   167.235.109.6
api.surveyl.top     A   167.235.109.6
info.surveyl.top    A   167.235.109.6
privacy.surveyl.top A   167.235.109.6
```

cert-manager will re-issue Let's Encrypt certificates automatically on the next Ingress reconcile.

---

## What Was NOT Changed

| File/Resource | Status |
|---|---|
| `charts/mtg-int/` | Untouched — full Helm chart intact |
| `docker-compose.int.yml` | Untouched — still works for local dev |
| `scripts/start-integration.sh` | Untouched |
| `~/hetzner-k3s-cluster/` | Cluster config and kubeconfig intact |
| K8s cluster itself | Still running at `91.107.233.123` (costs money — decommission when sure) |

The K8s cluster continues to incur costs until explicitly deleted via hetzner-k8s.
Once you are confident in the single-server setup, delete it:

```bash
cd ~/hetzner-k3s-cluster
hetzner-k3s delete --config cluster_config.yaml
```
