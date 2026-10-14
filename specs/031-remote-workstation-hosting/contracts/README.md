# Contracts: Remote Workstation Hosting (031)

**Date**: 2026-10-14

## REST API — unchanged

This feature does **not** change the workstation REST contract. The hosted deployment serves exactly the endpoints already defined for the offline/identity features:

- `GET /health` — anonymous liveness (vector store + model status)
- `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me`
- `POST|GET /api/documents`, `GET /api/documents/{id}/content`, `GET /api/documents/mine`, `DELETE /api/documents/{id}`
- `POST /api/queries`, `GET /api/queries/history`, `GET /api/queries/{id}`
- `GET|POST /api/users`, `GET|PATCH /api/users/{id}`, `POST /api/users/{id}/reset-password`

The authoritative schema remains [`specs/001-offline-mode/contracts/api.yaml`](../../001-offline-mode/contracts/api.yaml); contract tests are unchanged. The only difference in a hosted deployment is the transport (public HTTPS on 443) and the server's `servers:` URL, not the paths, methods, payloads, or status codes.

## Deployment interface — new

The **new** interface this feature defines is the deployment/configuration contract between the operator, the host, and the client. It is documented in [deployment.md](deployment.md): required ports, environment variables, volume paths, and the client configuration keys. Treat that file as the contract for the `deploy/` artifacts and the operator runbook.

### Why a deployment contract and not an API contract

The deliverable is operational (a droplet, a Compose stack, a client config template, a runbook), so its "interface" is the set of inputs an operator must supply and the guarantees the deployment must hold (only 443 public, all state on a volume, no per-deployment source edits). Encoding it here lets the runbook, the Compose file, and the tasks each be validated against one source of truth.
