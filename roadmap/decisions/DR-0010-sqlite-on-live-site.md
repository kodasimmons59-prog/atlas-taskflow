# DR-0010 Run the live site on SQLite; keep SQL Server on the PC

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** Assignment 6 asks for sign-in to work in the deployed app, and sign-in needs a database for accounts.
Azure for Students refused to create an Azure SQL server in every region in Sprint 5 (DR-0009). Koda chose SQLite for the
live site ([OV-008](../rulings/OV-008-sqlite-on-live-site.md)).

**Decision:** the database engine is a setting, `Database:Provider`. `appsettings.json` (the live site) says `Sqlite`;
`appsettings.Development.json` (Koda's PC) says `SqlServer` with LocalDB, as in Sprint 5. On App Service the SQLite file
is `/home/data/atlas-taskflow.db`, on the part of the disk that survives restarts and deployments. SQLite gets its own
migrations (`Data/Migrations/Sqlite`, through the `SqliteAtlasDbContext` subclass) because EF Core migrations are written
for one engine. The tables and rules are shared. Data errors are recognised from both engines (`DataErrors`).

**Consequences:** the live site gets real accounts and saved data with nothing new to create in Azure. SQLite is fine for
a class project with a handful of users on one App Service instance, but it isn't built for many people writing at once,
and `/home` on App Service is a network share, so this is not the long-term production database: PLAN-004 revisits it.
The CI tests still run on SQL Server, plus one test (`SqliteTests`) that starts the app the way the live site does.
Supersedes DR-0009.
