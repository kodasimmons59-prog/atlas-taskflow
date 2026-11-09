# DR-0012 Enforce team roles on the server with policies and service checks; hide other teams' data

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** Assignment 6 says hiding a button is not authorization. The Sprint 3 design gives three permission levels
(FR-3.1 to FR-3.3, FT-10): Team Leader, delegated manager, and Team Member, and these differ by team: one person can lead
one team and be a member of another (FR-1.3).

**Decision:** two authorization policies, checked before an action runs: `TeamManager` (leader or delegated manager:
create, edit, delete and reassign tasks, add reference files, request changes, create projects) and `TeamLeader`
(invite people, grant or remove manager permissions). A refused request gets HTTP 403 and a plain "You don't have
permission to do that" page. `TaskWorkflowService` checks the role again, plus the rules that depend on the task
(only assignees submit work or move their tasks; only managers complete), so a missed attribute can't open a hole.
Every lookup by id also checks the record belongs to the current team; if not, the answer is 404 "not found", the same
as for an id that doesn't exist, so a changed number in the address reveals nothing. The selected team is only accepted
from the cookie if the person is a member. Invited people join as standard members (least privilege).

**Consequences:** the rules live in one place per kind (policies for "who are you in this team", the service for "what
can you do to this task"), and the security tests in docs/SECURITY.md check them by sending requests directly, not through the buttons.
