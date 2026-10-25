# Atlas TaskFlow Roadmap

The single source of truth for **what is planned and what state it is in**. Everything else (design, decisions, owner
rulings, evidence) lives in its own file; this roadmap points to those files and does not repeat them.

Owner: Koda Simmons · Last updated: 2026-10-25

## §0 How to use this file

- **Authority:** this file owns plan and status only. Details live in the files it links to.
- **Birth:** a new plan is added to the register (§4) in the same commit that creates its file.
- **Maintenance:** when a plan's status changes, this file changes in the same commit.
- **Status words** (no others are allowed; `scripts/roadmap-lint.py` checks):

| Word | Meaning |
| --- | --- |
| `HORIZON` | Expected, not yet planned in detail |
| `PLANNED` | Scoped and ready to start |
| `ACTIVE` | Being worked on |
| `BLOCKED` | Waiting on something outside the work; the plan says what |
| `REVIEW` | Work finished, waiting for the owner to accept it |
| `DONE` | Accepted by the owner |
| `DEFERRED` | Moved to a later time on purpose |
| `DROPPED` | Not going to happen; the plan says why |
| `SUPERSEDED` | Replaced by another plan, which it names |

## §1 Where we've been

| Sprint | Outcome | Record |
| --- | --- | --- |
| 1 | Problem & Product Brief: small businesses juggle email, calls and old tools to track work | Course submission (Brief) |
| 2 | SaaS Product Analysis: Atlas as a multi-tenant, subscription SaaS | Course submission (Assignment 2) |
| 3 | Product Design Package: personas, journeys, user stories, five-status workflow, Azure architecture | Course submission (Assignment 3) |
| 4 | Working app built, tested and deployed to Azure App Service; submitted | [PLAN-001](plans/PLAN-001-working-cloud-app.md), git history from `00e7324` |

## §2 Where we are

Sprint 5. Assignment 4 is submitted. Atlas is moving from in-memory data to a SQL Server database through Entity
Framework Core ([PLAN-002](plans/PLAN-002-persistent-data.md)).

## §3 Where we're going

Later sprints, as listed in the course briefs: security (Sprint 6), production readiness (Sprint 7),
and the Version 1.0 release (Sprint 8). Each is a `HORIZON` plan in §4 until its assignment is released.

## §4 Plan register

| Plan | Title | Sprint | Status |
| --- | --- | --- | --- |
| [PLAN-001](plans/PLAN-001-working-cloud-app.md) | Working cloud application | 4 | `DONE` |
| [PLAN-002](plans/PLAN-002-persistent-data.md) | Persistent data with EF Core and SQL Server | 5 | `ACTIVE` |
| [PLAN-003](plans/PLAN-003-security-and-sign-in.md) | Sign-in, invitations and authorization | 6 | `HORIZON` |
| [PLAN-004](plans/PLAN-004-production-readiness.md) | Production readiness | 7 | `HORIZON` |
| [PLAN-005](plans/PLAN-005-version-1-release.md) | Version 1.0 release | 8 | `HORIZON` |

## §5 Decisions and rulings

- Decision records (why we chose what we chose): [decisions/INDEX.md](decisions/INDEX.md)
- Owner rulings (Koda's instructions, word for word): [rulings/INDEX.md](rulings/INDEX.md)

## §6 History of this file

Changes are recorded in [CHANGELOG.md](../CHANGELOG.md).
