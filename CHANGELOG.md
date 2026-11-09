# Changelog

Notable changes to Atlas TaskFlow and its roadmap, newest first.

## 2026-11-09

### Added (Sprint 6)
- Sign-up, sign-in, sign-out, email confirmation and password reset with ASP.NET Core Identity: hashed passwords, a
  password policy, lockout after 5 wrong passwords, and an `HttpOnly`/`Secure`/`SameSite` sign-in cookie (DR-0011).
- Every page requires sign-in except the account pages, error pages and `/health`.
- `TeamManager` and `TeamLeader` authorization policies on every manager and leader action, with a "not allowed" (403)
  page; the workflow service still checks too. Other teams' records answer 404 (DR-0012).
- Team creation and email-address invitations accepted on a new "My teams" page (DR-0014).
- Account emails go to a log, and on a developer PC to a developer mailbox page (DR-0013).
- Sample people get accounts with a demo password from configuration (DR-0015).
- SQLite support with its own migrations; the live site uses it so sign-in works there (DR-0010, OV-008).
- Browser security headers (`nosniff`, no framing, referrer policy).
- 42 new tests (96 in total), and [SECURITY.md](docs/SECURITY.md) with the security test matrix. The smoke test now
  checks that team pages need sign-in and that the developer mailbox doesn't exist live.

### Changed
- PLAN-002 is `DONE`: Koda submitted and accepted Assignment 5 (OV-009). PLAN-003 is `ACTIVE` (OV-007).
- DR-0004 ("Viewing as" switcher) is superseded by DR-0011 and the switcher is removed; DR-0009 is superseded by DR-0010.

### Fixed
- Signing up with the email of an existing person without an account could have linked to that person; sign-up now
  refuses it ([dev log](docs/DEV-LOG.md) §6).

## 2026-10-26

### Changed
- Azure for Students refused to create an Azure SQL server in any region, so Sprint 5 finishes on LocalDB and the live
  database moves to PLAN-004 (OV-006, DR-0009). The live site stays on the Sprint 4 build for now.
- Koda's restart test on LocalDB passed and the Assignment 5 submission is complete; PLAN-002 is `REVIEW`.

## 2026-10-25

### Added (Sprint 5)
- Atlas stores its data in SQL Server through Entity Framework Core: ten tables, the first migration, and sample data
  added once to an empty database (DR-0006, DR-0008). Files are kept in the database (DR-0007).
- Friendly handling of database errors: a rejected save becomes a message on the page, and an unreachable database
  shows a 503 page without technical details. `/health` now checks the database.
- Tests run against real SQL Server (LocalDB locally, a container in CI); 10 new tests for CRUD, validation, errors and
  persistence after a restart. 54 tests in total.
- Docs: [DATA-MODEL.md](docs/DATA-MODEL.md), Azure SQL setup in DEPLOYMENT.md, Sprint 5 evidence in TESTING.md, dev log
  entries 3 to 5.

### Changed
- PLAN-001 is `DONE`: Assignment 4 was submitted. PLAN-002 is `ACTIVE` and now uses SQL Server with files kept in the
  database instead of Blob Storage (OV-004, OV-005).

### Added
- First Azure deployment, live at https://atlas-taskflow-koda-d8gjabatgvatgtcj.northcentralus-01.azurewebsites.net; smoke test passed.
- Azure App Service `atlas-taskflow-koda` (North Central US) and the GitHub deploy settings; PLAN-001 is `ACTIVE` again.
- Deployment guide notes on Azure for Students region and resource group limits.
- Roadmap system based on Yardgoat Caboose: `roadmap/ROADMAP.md`, plans PLAN-001 to PLAN-005, decision records
  DR-0001 to DR-0005, owner rulings OV-001 to OV-003, `CLAUDE.md` working rules, and `scripts/roadmap-lint.py`, which
  now runs in CI.
- Sprint 4 working application (PLAN-001), built in these commits:
  - `00e7324` Scaffold ASP.NET Core MVC app and xUnit test project
  - `21aed01` Domain models from the Sprint 3 data design
  - `9b50ead` In-memory store, sample data and task workflow rules, with unit tests
  - `c807c80` Dashboard, tasks, project boards, team and notifications screens
  - `998dd09` Integration tests
  - `1b4f71e` Azure deploy workflow, health check and project docs

### Fixed
- `998dd09` An unknown status value fell back to Not Assigned and unassigned the task ([dev log](docs/DEV-LOG.md) §1).
