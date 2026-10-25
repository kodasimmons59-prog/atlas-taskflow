# DR-0002 Keep data in memory for Sprint 4

Date: 2026-10-25 · Status: superseded by [DR-0006](DR-0006-ef-core-sql-server.md) · Plan: PLAN-001

**Context:** Assignment 4 says a production database is not the goal yet. Setting up Azure SQL now would add cost and
setup time without helping the Sprint 4 workflows.

**Decision:** `AtlasStore` holds data in memory, shaped like the planned tables, seeded with sample teams. Uploaded files
are kept in memory with a 5 MB limit each.

**Consequences:** data resets on restart, and the App Service must stay at one instance. Replaced in PLAN-002.
