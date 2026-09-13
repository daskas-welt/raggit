# 004-identity Implementation TODO

## Phase 1: Setup

- [ ] T002 Configure `Auth:*` defaults in `appsettings.json`

## Phase 2: Foundational

- [ ] T003 Amend Constitution VI 1.1.0 → 1.2.0 (local per-person accounts)
- [ ] T004 Create `User.cs` entity
- [ ] T005 Implement `PasswordHasher.cs` (PBKDF2)
- [ ] T006 Extend `RagDbContext` with `Users` DDL
- [ ] T007 Add `UserStore.cs`
- [ ] T008 Configure `Program.cs` JwtBearer HS256 + OnTokenValidated + HTTPS
- [ ] T009 Add `JwtTokenService.cs`

## Follow-ups

- [ ] [Priority: Low] Final cleanup `dotnet csharpier check .` and full `dotnet test` after T042.
