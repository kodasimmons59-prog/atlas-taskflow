# DR-0006 Store data in SQL Server with Entity Framework Core

Date: 2026-10-25 · Status: accepted · Plan: PLAN-002

**Context:** Assignment 5 asks for Entity Framework Core and a relational database. Koda develops on Windows with Visual
Studio and chose SQL Server ([OV-005](../rulings/OV-005-assignment-5-answers.md)). The Sprint 3 architecture named Azure
SQL for the live site.

**Decision:** EF Core 8 with the SQL Server provider. Development uses SQL Server LocalDB, which comes with Visual
Studio; the live site uses an Azure SQL Database (free offer, auto-pause at the free limit). The connection string is
`DefaultConnection`: in `appsettings.Development.json` for LocalDB, and an App Service connection string setting on
Azure, so no password is in the repo. Connections retry on transient failures, which covers Azure SQL resuming from
auto-pause. Tests run against real SQL Server too (LocalDB locally, a SQL Server container in CI), so they exercise the
same engine and constraints as production.

**Consequences:** one database engine everywhere, so the migrations work unchanged in every environment. The first
request after the database has auto-paused can take up to a minute. Replaces [DR-0002](DR-0002-in-memory-data.md).
