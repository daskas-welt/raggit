# 004-identity Implementation TODO

## Phase 1: Setup



## Phase 2: Foundational


## Follow-ups

- [ ] [Priority: Low] Override `Auth:JwtSigningKeyPath` in `TestApiFactory` to a temp path and inject it into `JwtTokenServiceOptions` so contract tests do not share `data/auth.key`.
- [ ] [Priority: Low] Document or unify the `[Authorize]` + `[Authorize(Roles = "Admin")]` pattern so future Admin-only controllers inherit both ApiKey and Bearer schemes (Ref: src/RAGGit.Workstation.Api/Controllers/UsersController.cs, DocumentsController.cs).

(End of file - total 13 lines)
