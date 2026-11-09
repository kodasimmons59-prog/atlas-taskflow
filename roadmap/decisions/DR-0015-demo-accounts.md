# DR-0015 Sample accounts share a demo password kept out of the live code

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** the instructor needs to sign in as a leader and a member to check the access rules. The sample people
(Sarah, Marcus, Priya, Leo) existed from Sprint 5 without accounts. Koda chose one shared demo password, given in the
submission and README but not on the live sign-in page ([OV-009](../rulings/OV-009-assignment-6-answers.md)).

**Decision:** at start-up, `DemoAccounts` gives each sample person a confirmed account if they don't have one, with the
password from the `DemoAccounts:Password` setting. It is in `appsettings.Development.json` for the PC; on Azure it's an
App Service setting (`DemoAccounts__Password`), so the code that's deployed holds no password. With no setting, no
sample accounts are made. Existing accounts are never changed. The development sign-in page mentions the sample emails;
the live one doesn't.

**Consequences:** the sample data can be demonstrated on both the PC and the live site. Anyone who knows the demo
password can sign in as the sample people, which is acceptable for made-up data, and is a reason to remove or change
these accounts before real customers use Atlas (PLAN-004).
