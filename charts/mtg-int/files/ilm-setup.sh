#!/bin/sh
set -e

curl_flags="--silent --show-error --cacert /certs/ca.crt -u $ES_USER:$ES_PASS"

for i in $(seq 1 20); do
  if curl $curl_flags "$ES_HOST/_cluster/health?wait_for_status=yellow&timeout=5s" >/dev/null; then
    break
  fi
  sleep 3
done

curl $curl_flags "$ES_HOST/_cluster/health?wait_for_status=yellow&timeout=5s" >/dev/null

request() {
  url="$1"
  data="$2"
  response_file="$(mktemp)"
  code="$(curl $curl_flags -w "%{http_code}" -o "$response_file" -H "Content-Type: application/json" -X PUT "$url" -d "$data")"
  if [ "$code" -ge 200 ] && [ "$code" -lt 300 ]; then
    rm -f "$response_file"
    return 0
  fi
  echo "Request failed: $url (HTTP $code)"
  cat "$response_file"
  rm -f "$response_file"
  return 1
}

request "$ES_HOST/_ilm/policy/mtg25-api-int-ilm" \
  '{"policy":{"phases":{"hot":{"actions":{}},"delete":{"min_age":"180d","actions":{"delete":{}}}}}}'

request "$ES_HOST/_index_template/mtg25-api-int-template" \
  '{"index_patterns":["mtg25-api-int-*"],"priority":200,"template":{"settings":{"index.lifecycle.name":"mtg25-api-int-ilm"}}}'
