# PLAN-002 Persistent data with EF Core and SQL Server

Status: `ACTIVE` · Sprint 5 · Source: Assignment 5 brief ([OV-004](../rulings/OV-004-complete-assignment-5.md))

## Goal

Replace the in-memory `AtlasStore` with Entity Framework Core on SQL Server so Atlas remembers its data after a
restart, then prove it. Follows from [DR-0002](../decisions/DR-0002-in-memory-data.md).

## Scope

In: entity classes and relationships from the Sprint 3 data design, a DbContext, migrations, SQL Server LocalDB for
development, create/read/update/delete through the existing screens,
validation and database constraints, friendly handling of database errors, a persistence-after-restart test, testing
evidence and the Word submission.

Out: the live Azure SQL database (blocked by the Azure for Students region policy; moved to PLAN-004,
[DR-0009](../decisions/DR-0009-live-database-deferred.md)), Azure Blob Storage (files stay in the database, [OV-005](../rulings/OV-005-assignment-5-answers.md)), sign-in and
authorization (PLAN-003), production database operations such as backups and scaling (PLAN-004).

## Steps

| Step | State |
| --- | --- |
| Koda's answers on database, tools, files, sample data and submission | Done ([OV-005](../rulings/OV-005-assignment-5-answers.md)) |
| EF Core data layer, migrations, validation and error handling | Done |
| Tests against a real SQL Server, including a restart test | Done: 54 passing; local browser restart check ([TESTING.md](../../docs/TESTING.md) §5) |
| Azure SQL Database created and connected to the App Service | Dropped from this plan: Azure refused every region; moved to PLAN-004 ([OV-006](../rulings/OV-006-finish-with-localdb.md)) |
| Run on Koda's PC with LocalDB, restart check and screenshots | Done: stopped 10:17 PM, restarted 10:18 PM, task kept |
| Submission document in the Assignment 4 style; reflection in Koda's words | Document drafted; reflection waiting on Koda |

## Decisions

[DR-0006](../decisions/DR-0006-ef-core-sql-server.md), [DR-0007](../decisions/DR-0007-files-in-database.md),
[DR-0008](../decisions/DR-0008-migrate-and-save-per-action.md), [DR-0009](../decisions/DR-0009-live-database-deferred.md). Data model: [DATA-MODEL.md](../../docs/DATA-MODEL.md).

## Done when

Atlas runs on LocalDB on Koda's PC, data survives a restart there, the tests pass in CI, and Koda accepts the
submission.
