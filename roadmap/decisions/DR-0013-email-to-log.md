# DR-0013 Write account emails to a log until Atlas has an email service

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** email confirmation (FR-1.1) and password reset (FR-1.2) send links by email. Sending real email needs an
email service account (for example SendGrid) that Koda would have to set up. Koda chose to wait
([OV-009](../rulings/OV-009-assignment-6-answers.md)).

**Decision:** the confirmation and reset flows are built in full, but the `IEmailSender` used is `LogEmailSender`. On a
developer PC (Development) it logs the email with its link and keeps the latest ones for a developer mailbox page at
`/dev/mailbox`. On the live site it logs only that an email would have been sent, never the link, because the link is a
one-time key to the account; `/dev/mailbox` is a 404 there.

**Consequences:** on the live site, new sign-ups can sign in but can't confirm their email, so they can't create or join
a team yet; the sample accounts are already confirmed. Adding a real sender later means one new `IEmailSender` class and
a setting; nothing else changes. Moves to PLAN-004.
