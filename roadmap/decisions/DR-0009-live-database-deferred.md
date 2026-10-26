# DR-0009 Defer the live database; Sprint 5 runs on LocalDB

Date: 2026-10-26 · Status: accepted · Plan: PLAN-002

**Context:** creating an Azure SQL server failed in every region with `RequestDisallowedByAzure`, the Azure for Students
region policy, even for regions the subscription's own policy lists as allowed (northcentralus, westus2). Lifting it
needs Azure support or the school. Assignment 5 asks for a database "appropriate for the development environment" and
leaves production database operations to a later sprint.

**Decision:** Sprint 5 is completed and evidenced on SQL Server LocalDB on Koda's PC
([OV-006](../rulings/OV-006-finish-with-localdb.md)). The Sprint 5 code is not merged to `main` yet, because every push to
`main` deploys, and the live App Service has no database to connect to. The live site stays on the Sprint 4 build until
a live database exists. Setting one up moves to PLAN-004.

**Consequences:** no live persistence evidence this sprint. The code, migrations and Azure SQL steps in DEPLOYMENT.md are
ready, so once a database can be created, going live is: create it, add the connection string, merge.
