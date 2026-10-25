# PLAN-002 Persistent data with EF Core and SQL Server

Status: `ACTIVE` · Sprint 5 · Source: Assignment 5 brief ([OV-004](../rulings/OV-004-complete-assignment-5.md))

## Goal

Replace the in-memory `AtlasStore` with Entity Framework Core on SQL Server so Atlas remembers its data after a
restart, then prove it. Follows from [DR-0002](../decisions/DR-0002-in-memory-data.md).

## Scope

In: entity classes and relationships from the Sprint 3 data design, a DbContext, migrations, SQL Server LocalDB for
development and Azure SQL Database for the live site, create/read/update/delete through the existing screens,
validation and database constraints, friendly handling of database errors, a persistence-after-restart test, testing
evidence and the Word submission.

Out: Azure Blob Storage (files stay in the database, [OV-005](../rulings/OV-005-assignment-5-answers.md)), sign-in and
authorization (PLAN-003), production database operations such as backups and scaling (PLAN-004).

## Steps

| Step | State |
| --- | --- |
| Koda's answers on database, tools, files, sample data and submission | Done ([OV-005](../rulings/OV-005-assignment-5-answers.md)) |
| EF Core data layer, migrations, validation and error handling | In progress |
| Tests against a real SQL Server, including a restart test | Not started |
| Azure SQL Database created and connected to the App Service | Waiting on Koda (portal steps) |
| Deploy and live persistence check | Not started |
| Submission document in the Assignment 4 style; reflection in Koda's words | Not started |

## Done when

The live site reads and writes Azure SQL, data survives an App Service restart, the tests pass in CI, and Koda accepts
the submission.
