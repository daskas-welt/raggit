# DRAFT (T031): Constitution amendment — .NET 8 → .NET 10 wording

> Status: DRAFT — no `gh` CLI or token on this machine, so this could not be
> filed as a GitHub issue. Owner: paste into a new issue on
> `daskas-welt/raggit` (or run `gh issue create -F` with this file), then
> delete this draft.

## Title

Constitution amendment (PATCH): update .NET 8 references to .NET 10 after 014-net10-upgrade

## Body

After `014-net10-upgrade` lands, `.specify/memory/constitution.md` still names
the old runtime in:

- Principle II (Workstation-Owned AI): "ASP.NET Core .NET 8"
- Principle III (.NET Library-First & Client Reuse): "`net8.0-windows10.0.17763.0`"
- Technology & Deployment Constraints stack: "C# .NET 8"

Proposed change: wording-only PATCH (`.NET 8` → `.NET 10`,
`net8.0-windows10.0.17763.0` → `net10.0-windows10.0.17763.0`). No principle
redefinition; single-tenant, offline invariant, citation grounding, and
test-first gates are unaffected. Historical specs (001–013) are unaffected —
they describe behavior, not toolchain versions.

Acceptance: constitution version bumped per Governance (PATCH), amendment
section appended, `/speckit.plan` Constitution Check passes verbatim on the
next feature.

## Related follow-ups (same filing session, separate issues or checklist)

- NU1903 transitive advisories surfaced by the .NET 10 SDK audit
  (`System.IO.Packaging 8.0.0` via DocumentFormat/ClosedXML) — consider
  targeted bumps outside this migration.
- T016/T024 deferrals: docker image build+serve and full interactive WinUI
  smoke need a docker host / signed-MSIX install path.
