# Changelog

Notable changes to Atlas TaskFlow and its roadmap, newest first.

## 2026-10-25

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
