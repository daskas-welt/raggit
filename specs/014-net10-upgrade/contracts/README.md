# Contracts: .NET 10 Upgrade (014-net10-upgrade)

**No contract changes.** This migration alters build targeting only; every external interface is byte-for-byte unchanged:

- **HTTP API** — `POST /api/query`, `GET/POST /api/documents`, `/api/users`, auth endpoints: unchanged. Canonical definition remains in the per-spec `contracts/api.yaml` files; contract tests (`tests/contract`) run unmodified as the proof (FR-002).
- **CLI surface** (`OperatorCli`, library CLIs) — text in/out and JSON shapes unchanged.
- **MSIX package identity** (name, publisher, version scheme) — unchanged; only the build SDK/TFM changes.
- **Container interface** — port `5001`, env vars, volume mounts unchanged; only base/SDK image tags change.

If implementation discovers any contract-affecting change (e.g., a serializer behavior difference under the new runtime), it MUST be recorded here and treated as a scope breach requiring owner approval — per the spec, this migration promises zero behavior change.
