# PLAN-004 Production readiness

Status: `HORIZON` · Sprint 7

## Goal

Production-readiness testing and hardening, as described in the Sprint 7 assignment.

Carried over from earlier sprints:

- The live site runs on SQLite from Sprint 6 ([DR-0010](../decisions/DR-0010-sqlite-on-live-site.md)). A production
  database (Azure SQL, if Azure for Students allows it by then, see [DR-0009](../decisions/DR-0009-live-database-deferred.md)
  and [DEPLOYMENT.md](../../docs/DEPLOYMENT.md)) is worth revisiting.
- Real email for confirmation, password reset and invitations ([DR-0013](../decisions/DR-0013-email-to-log.md)).
- Retire or change the shared demo password before real customers use Atlas
  ([DR-0015](../decisions/DR-0015-demo-accounts.md)).

Details are planned when the sprint's assignment is released.
