#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

docker compose build

for service in orders payment orders-payment-route; do
  image="mavis-studio-live-seed-${service}:local"
  digest="$(docker image inspect --format '{{.Id}}' "$image")"
  printf '%-24s %s\n' "$service" "$digest"
done

echo
echo "Local image IDs are printed above. For a remote Runner, push each image"
echo "to your registry and use repository@sha256:<digest> in Compose."
