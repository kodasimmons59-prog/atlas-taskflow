# DR-0007 Keep uploaded files in the database

Date: 2026-10-25 · Status: accepted · Plan: PLAN-002

**Context:** the Sprint 3 architecture put files in Azure Blob Storage. Assignment 5 is about persistence through EF
Core, and Koda chose to keep files in the database ([OV-005](../rulings/OV-005-assignment-5-answers.md)).

**Decision:** `Attachments.Content` is a `varbinary(max)` column. The existing 5 MB per file and 5 files per upload limits
stay. Listing a task's files doesn't load their contents; only a download does.

**Consequences:** one Azure resource fewer, and a task's files are saved in the same transaction as the status change
they belong to. Files count against the free database's 32 GB limit. Moving to Blob Storage later only changes
`AtlasStore` and the download action.
