#!/usr/bin/env bash
set -euo pipefail

# Simple single-request latency probe using curl.
# Usage:
#   ./scripts/latency-check.sh "http://localhost:5286/api/collections/2/cards?search=sha&pageSize=6&pageIndex=1" 5
#   ./scripts/latency-check.sh "http://localhost:5286/api/collections/2/cards" 3 POST '{"search":"sha"}'
#   ./scripts/latency-check.sh "http://localhost:5286/api/decks/import" 5 POST @body.json
#   METHOD=PUT BODY='{"name":"test"}' ./scripts/latency-check.sh "http://localhost:5286/api/collections/2" 1
#   BODY_FILE=body.json ./scripts/latency-check.sh "http://localhost:5286/api/decks/import" 5 POST
#   AUTH_TOKEN="..." ./scripts/latency-check.sh "http://localhost:5286/api/health"
#
# If AUTH_TOKEN is not provided, the script will login to obtain one using:
#   LOGIN_URL (default: http://localhost:5286/api/accounts/login)
#   LOGIN_EMAIL (default: alessandro23@gmail.com)
#   LOGIN_PASSWORD (default: Password!12dd3)

url="${1:-}"
repeats="${2:-1}"
method_arg="${3:-}"
body_arg="${4:-}"

if [[ -z "$url" ]]; then
  echo "Usage: AUTH_TOKEN=... $0 <url> [repeats]" >&2
  exit 1
fi

if ! [[ "$repeats" =~ ^[0-9]+$ ]]; then
  echo "Repeats must be a positive integer." >&2
  exit 1
fi

method="${METHOD:-${method_arg:-GET}}"
body="${BODY:-${body_arg:-}}"
body_file="${BODY_FILE:-}"

if [[ -n "$body_file" ]]; then
  body="$(cat "$body_file")"
elif [[ "$body" == @* ]]; then
  body="$(cat "${body#@}")"
fi

headers=("-H" "Accept: application/json" "-H" "Content-Type: application/json")
auth_token="${AUTH_TOKEN:-}"
if [[ -z "$auth_token" ]]; then
  login_url="${LOGIN_URL:-http://localhost:5286/api/accounts/login}"
  login_email="${LOGIN_EMAIL:-alessandro23@gmail.com}"
  login_password="${LOGIN_PASSWORD:-Password!12dd3}"

  login_payload=$(printf '{"email":"%s","password":"%s"}' "$login_email" "$login_password")
  login_response=$(curl -s --location "$login_url" \
    -H "Content-Type: application/json" \
    --data-raw "$login_payload")

  python_bin="python"
  if command -v python3 >/dev/null 2>&1; then
    python_bin="python3"
  fi

  auth_token=$(printf '%s' "$login_response" | "$python_bin" -c '
import json, sys
data = sys.stdin.read()
try:
    payload = json.loads(data)
except json.JSONDecodeError:
    print("", end="")
    sys.exit(0)
token = (
    payload.get("token")
    or payload.get("accessToken")
    or payload.get("jwt")
    or (payload.get("data") or {}).get("token")
    or (payload.get("data") or {}).get("accessToken")
    or (payload.get("result") or {}).get("token")
    or (payload.get("result") or {}).get("accessToken")
)
print(token or "", end="")
')
fi

if [[ -n "$auth_token" ]]; then
  headers+=("-H" "Authorization: Bearer ${auth_token}")
else
  echo "Warning: login did not return a token; proceeding without Authorization header." >&2
  echo "Login response was: $login_response" >&2
fi

for ((i=1; i<=repeats; i++)); do
  curl_args=(--location "$url" -X "$method" "${headers[@]}")
  if [[ -n "$body" ]]; then
    curl_args+=(--data-raw "$body")
  fi
  curl -s -o /dev/null \
    -w "run=${i} dns=%{time_namelookup} connect=%{time_connect} ttfb=%{time_starttransfer} total=%{time_total}\n" \
    "${curl_args[@]}"
done
