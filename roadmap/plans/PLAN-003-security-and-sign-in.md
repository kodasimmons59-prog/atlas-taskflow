# PLAN-003 Sign-in, invitations and authorization

Status: `ACTIVE` · Sprint 6 · Source: Assignment 6 brief ([OV-007](../rulings/OV-007-complete-assignment-6.md))

## Goal

Give Atlas a security foundation: people sign in with their own accounts (authentication), Atlas knows who is making
each request (identity), and the server enforces who may see and change what (authorization, roles, data isolation),
then prove it with tests from both the allowed and the refused side. Replaces the demo switcher
([DR-0004](../decisions/DR-0004-demo-identity-switcher.md)).

## Scope

In: sign-up, sign-in, sign-out, email confirmation and password reset (FT-01, FR-1.1, FR-1.2) with ASP.NET Core
Identity; every page behind sign-in; Team Leader, delegated manager and Team Member permissions enforced on the server
(FR-3.1 to FR-3.3, FT-10); team data isolation; team creation and email-address invitations (US-02, US-03, FT-02); clear
"sign in" and "not allowed" pages; the live site on SQLite so sign-in works there; security tests and a test matrix;
the Word submission with Koda's reflection.

Out: sending real email (DR-0013, moves to PLAN-004), two-factor sign-in, removing people from a team, account
deletion, penetration testing (the brief says it isn't expected), Azure SQL (PLAN-004).

## Steps

| Step | State |
| --- | --- |
| Koda's answers: live database, email, invitations, demo accounts, submission ([OV-008](../rulings/OV-008-sqlite-on-live-site.md), [OV-009](../rulings/OV-009-assignment-6-answers.md)) | Done |
| Identity accounts, sign-in pages, lockout and secure cookie ([DR-0011](../decisions/DR-0011-identity-accounts.md)) | Done |
| Team role policies, service checks and team isolation ([DR-0012](../decisions/DR-0012-team-role-authorization.md)) | Done |
| Email confirmation and password reset through the log mailbox ([DR-0013](../decisions/DR-0013-email-to-log.md)) | Done |
| Team creation and invitations ([DR-0014](../decisions/DR-0014-in-app-invitations.md)) | Done |
| Sample accounts with a configured demo password ([DR-0015](../decisions/DR-0015-demo-accounts.md)) | Done |
| SQLite for the live site ([DR-0010](../decisions/DR-0010-sqlite-on-live-site.md)) | Done |
| Security tests and matrix ([SECURITY.md](../../docs/SECURITY.md)) | Done: 96 tests passing (42 new) |
| Koda's PC check in Visual Studio with LocalDB, and screenshots | Waiting for Koda |
| Azure setting for the demo password, merge to `main`, live check | Waiting for Koda |
| Submission document; reflection in Koda's words | Not started |

## Decisions

[DR-0010](../decisions/DR-0010-sqlite-on-live-site.md), [DR-0011](../decisions/DR-0011-identity-accounts.md),
[DR-0012](../decisions/DR-0012-team-role-authorization.md), [DR-0013](../decisions/DR-0013-email-to-log.md),
[DR-0014](../decisions/DR-0014-in-app-invitations.md), [DR-0015](../decisions/DR-0015-demo-accounts.md).
Security overview and test matrix: [SECURITY.md](../../docs/SECURITY.md).

## Done when

Sign-in and the access rules work on Koda's PC and on the live site, the tests pass in CI, the submission is complete
with Koda's reflection, and Koda accepts it.
