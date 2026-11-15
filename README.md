# MAVIS Studio Live — Microservice Seed

A small public repository used to exercise **MAVIS Studio Live** with a real Docker
Compose stack. It is intentionally simple: three .NET 8 services and one prepared
route-controller. There are no production secrets, host ports or privileged settings.

## Services

| Service | Runtime | Port | Endpoints | Purpose |
|---|---|---|---|---|
| `orders` | .NET 8 (`dotnet8-linux-x64-glibc`) | 8080 | `GET /health`, `GET /orders`, `GET /failed-order` | Entry point. Calls `PAYMENT_URL`, which points at the route-controller. |
| `payment` | .NET 8 (`dotnet8-linux-x64-glibc`) | 8080 | `GET /health`, `GET /pay`, `GET /fail` | Downstream dependency. |
| `orders-payment-route` | .NET 8 (`dotnet8-linux-x64-glibc`) | 8080 | `GET /health`, forwards `GET`/`HEAD` to `UPSTREAM_URL` | Prepared connection controller for the `orders → payment` wire. |

The Compose network is internal. The services are reached by container DNS, not by
host port mappings.

```text
orders ──HTTP──> orders-payment-route ──HTTP──> payment
```

When `payment` and `orders-payment-route` are stopped, `GET /orders` fails. Starting
the route-controller restores the real call path. This is the behaviour Studio Live
uses for prepared wires.

## Run locally

```bash
docker compose up -d --build
docker run --rm --network mavis-studio-live-seed_default curlimages/curl:8.10.1 \
  -sS http://orders:8080/orders
docker run --rm --network mavis-studio-live-seed_default curlimages/curl:8.10.1 \
  -sS -i http://orders:8080/failed-order
```

Expected output while the route-controller is running:

- `/orders` returns `200` with a `payment` body.
- `/failed-order` returns the upstream `503` from `payment`.

Stop the route-controller to see the real failure:

```bash
docker compose stop orders-payment-route
docker run --rm --network mavis-studio-live-seed_default curlimages/curl:8.10.1 \
  -sS -i http://orders:8080/orders
```

## Use with MAVIS Studio Live

1. Add all three services to the reference project's **runtime applications**:
   - runtime: `dotnet8-linux-x64-glibc`
   - container port: `8080`
   - readiness path: `/health`
2. Add all three services to the **confirmed service mappings** for the model.
3. Save the prepared topology in the reference project's runtime profile. Use
   [`topology.example.json`](topology.example.json):

```json
{
  "version": 1,
  "blocks": [
    { "service": "orders", "label": "Order API", "canScale": true, "initiallyStopped": false },
    { "service": "payment", "label": "Payment API", "canScale": false, "initiallyStopped": true }
  ],
  "connections": [
    {
      "from": "orders",
      "to": "payment",
      "controllerService": "orders-payment-route",
      "protocol": "HTTP",
      "initiallyStopped": true
    }
  ]
}
```

4. Build images. For a local Runner, [`scripts/pin-images.sh`](scripts/pin-images.sh)
   builds the stack and prints the local image IDs. For a remote Runner, push the
   images to your registry and use `repository@sha256:<digest>` in Compose.

```bash
./scripts/pin-images.sh
```

5. Start an Interactive Studio session. The expected exercise is:

| Step | Action | Expected result |
|---|---|---|
| Baseline | payment and route are initially stopped | `GET /orders` fails |
| Add block | START `payment` | payment health becomes ready |
| Connect wire | START `orders-payment-route` | `GET /orders` returns 200 |
| Disconnect wire | STOP `orders-payment-route` | `GET /orders` returns 5xx |
| Connect again | START `orders-payment-route` | `GET /orders` returns 200 |
| Clone replica | SCALE `orders` to 2 | Runner confirms 2 replicas |

The controller must remain in the real call path. Do not point `orders` directly at
`payment`; that would bypass the prepared wire.

## Verification

Verified locally on 15/11/2026 with Docker Linux Engine:

- route-controller running: `GET /orders` returns **200**
- route-controller stopped: `GET /orders` returns **502**
- route-controller started again: `GET /orders` returns **200**

## Notes

- The seed uses the MAVIS Runner's .NET auto-instrumentation; no OTel package is
  required in the application code.
- `GET /health` is local to each service. It can succeed before the dependency is
  removed, which is required for full-stack readiness before the baseline workload.
- There are no fixed host ports, no `privileged`, no host network and no Docker socket
  mounts.
- This repository is a test fixture, not a production template.

## License

MIT — see [LICENSE](LICENSE).
