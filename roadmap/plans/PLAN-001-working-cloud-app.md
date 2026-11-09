# PLAN-001 Working cloud application

Status: `DONE` · Sprint 4 · Source: Assignment 4 brief

## Goal

Turn the Sprint 3 design into a working ASP.NET Core app, deployed to Azure App Service, with Git history, testing
evidence and a reflection.

## Scope

In: dashboard, projects and status boards, task create/edit/delete, the five-status workflow with the leader-only
completion gate, Not Assigned pool, file submission, discussion, notifications, team switcher, manager permissions.
Out (see later plans): database, sign-in, email.

## Steps

| Step | State |
| --- | --- |
| Build the app with models, controllers and views | Done (`00e7324`..`c807c80`) |
| Unit and integration tests | Done, 44 passing (`998dd09`) |
| Deploy workflow, smoke test, docs and screenshots | Done (`1b4f71e`) |
| Push to `kodasimmons59-prog/atlas-taskflow` | Done (`1d77b71`), first CI run green |
| Create the Azure App Service and add the deploy secret | Done: `atlas-taskflow-koda`, North Central US, Linux, Free F1 |
| First deploy and automated smoke test | Done (`d1ba462`), live at <https://atlas-taskflow-koda-d8gjabatgvatgtcj.northcentralus-01.azurewebsites.net> |
| Manual live checks | Koda, with the submission |
| Reflection | Koda wrote it |
| Submission | Done: Koda submitted Assignment 4 on 2026-10-25 ([OV-005](../rulings/OV-005-assignment-5-answers.md)) |

## Decisions

[DR-0001](../decisions/DR-0001-csharp-and-azure.md), [DR-0002](../decisions/DR-0002-in-memory-data.md),
[DR-0003](../decisions/DR-0003-polling-for-live-updates.md), [DR-0004](../decisions/DR-0004-demo-identity-switcher.md),
[DR-0005](../decisions/DR-0005-taskitem-name.md)

## Done when

The live Azure URL passes the smoke test, the live checks in TESTING.md are filled in, and Koda accepts the submission.
