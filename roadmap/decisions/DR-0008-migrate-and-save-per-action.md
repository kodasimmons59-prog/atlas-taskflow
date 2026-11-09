# DR-0008 Migrate on startup, and save once per user action

Date: 2026-10-25 · Status: accepted · Plan: PLAN-002

**Context:** the database schema has to match the code in every environment, and the sample teams should appear the
first time Atlas runs against an empty database. A user action such as "submit work" changes several tables at once.

**Decision:** on startup the app applies any pending migrations (`Database.Migrate()`) and adds the sample data if there
are no users. If the database can't be reached, startup logs the error and carries on so pages can show a friendly
"database unavailable" message. Inside a request, `AtlasStore` stages changes and each workflow action ends with one
`SaveChanges`, which SQL Server runs as a single transaction. Creating a record whose id is needed straight away (a new
task, project, team or user) is saved first.

**Consequences:** no manual database step when deploying. This is fine for one App Service instance; with several
instances, migrations would move to the deploy pipeline (PLAN-004). A failed save leaves nothing half-done.
