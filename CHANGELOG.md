# Changelog

Notable changes to Atlas TaskFlow and its roadmap, newest first.

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
