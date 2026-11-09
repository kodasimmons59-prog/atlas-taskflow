# Security and access control (Sprint 6)

How Atlas decides who you are, what you can do, and whose data you can see, and the tests that prove it.
The reasons behind each choice are in the decision records [DR-0010](../roadmap/decisions/DR-0010-sqlite-on-live-site.md)
to [DR-0015](../roadmap/decisions/DR-0015-demo-accounts.md).

## 1. Authentication: who are you?

- **Accounts** are ASP.NET Core Identity accounts in the Atlas database ([DR-0011](../roadmap/decisions/DR-0011-identity-accounts.md)).
  Each belongs to one Atlas person (the `Users` table that tasks and teams point to).
- **Passwords** are stored only as salted hashes. Rules: at least 8 characters, with upper case, lower case and a digit.
- **Sign-in** uses email and password. A wrong email and a wrong password get the same message, so the page can't be
  used to find out who has an account. **5 wrong passwords lock the account for 15 minutes.**
- **The sign-in cookie** (`atlas_auth`) is encrypted and signed by the server, `HttpOnly` (scripts can't read it),
  `Secure` (HTTPS only) and `SameSite=Lax`. It lasts 8 hours of activity, or longer with "Keep me signed in".
- **Sign-out** is a form post (with an anti-forgery token), so another site can't sign you out with a link.
- **Email confirmation** is needed before creating or joining a team (FR-1.1). **Password reset** sends a single-use
  link that expires in a day (FR-1.2). Until Atlas has an email service, these emails go to a log and, on a developer PC,
  to the developer mailbox at `/dev/mailbox` ([DR-0013](../roadmap/decisions/DR-0013-email-to-log.md)).
- **Sign-up** refuses an email that already belongs to an Atlas person, so nobody can sign up as a teammate.
- After sign-in Atlas only returns you to a page **inside Atlas**, never to another site named in the link.

## 2. Identity: how Atlas knows who is asking

`CurrentUserContext` reads the person's id from the signed-in identity on every request. It is the only place that
decides who the current user is and which team they're working in. The chosen team is remembered in a cookie, but it's
only accepted if the person really belongs to that team.

## 3. Authorization: what are you allowed to do?

Every page requires sign-in unless it is marked public. Public pages: sign in, create account, forgot/reset password,
confirm email, the error pages and `/health` (which only says Healthy or Unhealthy).

Roles belong to a team: someone can lead one team and be a member of another.

| Action | Team Member | Member with manager permissions | Team Leader |
| --- | :---: | :---: | :---: |
| See the team's dashboard, tasks, projects, team list | Yes | Yes | Yes |
| Comment on a task, download its files | Yes | Yes | Yes |
| Pick up an unassigned task | Yes | Yes | Yes |
| Move **their own** tasks between Assigned, In Progress and Needs Review; submit work | Yes | Yes | Yes |
| Move someone else's task, unassign a task | No | Yes | Yes |
| Approve (mark Completed) or reopen a completed task | No | Yes | Yes |
| Create, edit, reassign or delete tasks; add reference files; request changes | No | Yes | Yes |
| Create projects | No | Yes | Yes |
| Invite people, see and cancel invitations | No | No | Yes |
| Give or remove manager permissions | No | No | Yes |
| See or change another team's anything | No | No | No |

How it's enforced ([DR-0012](../roadmap/decisions/DR-0012-team-role-authorization.md)):

1. **Policies before the action runs.** `TeamManager` and `TeamLeader` policies (`Security/TeamPermissions.cs`) are on
   every manager and leader action. Someone without the role who types the address or posts the form directly gets
   **403** and a plain "You don't have permission to do that" page. Nothing runs.
2. **Checks in the workflow service.** `TaskWorkflowService` checks the role again, and the rules that depend on the
   task (only assignees submit work; only managers complete). This is the backstop if a policy is ever missed.
3. **Team isolation on every lookup.** Every task, project, file, notification and invitation is looked up together
   with the current team (and, for notifications and invitations, the current person). Anything else is **404 "not
   found"**, exactly as if it didn't exist, so changing a number in the address reveals nothing.
4. **Least privilege.** New team members join as standard members. Only the leader can raise someone's permissions.
   Managers can run tasks but can't change who has power in the team.

## 4. Other protections

- **Anti-forgery tokens** on every form post (from Sprint 4), so another site can't submit forms as you.
- **Browser headers** on every response: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` (no framing, which
  stops clickjacking) and `Referrer-Policy: strict-origin-when-cross-origin`. HTTPS is enforced with HSTS on the live
  site.
- **No secrets in the repo.** The live demo password (`DemoAccounts__Password`) and any database password are App
  Service settings ([DR-0015](../roadmap/decisions/DR-0015-demo-accounts.md)).
- **Plain error messages.** Security pages and errors never show stack traces, SQL or account details. Failed sign-ins
  and lockouts are logged for the developer, by account id rather than email.

## 5. Security test matrix

All of these are automated (`tests/AtlasTaskFlow.Tests/SecurityTests.cs`, `SqliteTests.cs`) and run on every push.
96 tests pass in total, 42 of them new in Sprint 6 (2026-11-09). Koda's manual checks are in [TESTING.md](TESTING.md) §6.

| # | Scenario | Expected result | Actual result | Pass/Fail |
| --- | --- | --- | --- | --- |
| S01 | Signed out, open 10 protected addresses (dashboard, tasks, a task, new task, projects, team, notifications, a file download, my teams) | Sent to the sign-in page, which remembers where you were going | Redirected to `/Account/SignIn?ReturnUrl=…` for all 10 | Pass |
| S02 | Signed out, post "approve task" directly | Refused, task unchanged | Sent to sign-in; task still Needs Review | Pass |
| S03 | Signed out, open sign-in, create account, forgot password, `/health` | Open to everyone | 200 for all 4 | Pass |
| S04 | Sign in as Marcus with the right password | Marcus's dashboard, shown as a Team Member | Dashboard with "Marcus Vance", "Team Member" | Pass |
| S05 | Sign in with a wrong password, and with an unknown email | Same message for both; still signed out | "That email and password don't match an account." for both; protected pages still redirect | Pass |
| S06 | 5 wrong passwords, then the right one | Account locked for 15 minutes | Lockout message; right password refused | Pass |
| S07 | Sign in from a link that returns to `/Team`, and from one that returns to `https://evil.example` | Back to `/Team`; never to another site | `/Team`; the outside link went to the dashboard instead | Pass |
| S08 | Sign out, then open Tasks | Signed out; sign-in needed again | "You've signed out."; Tasks redirects to sign-in | Pass |
| S09 | Inspect the sign-in cookie | HttpOnly, Secure, SameSite, no readable user details | All three flags; no email in the cookie | Pass |
| S10 | Inspect response headers | nosniff and no framing | Both present | Pass |
| S11 | Sign up as a new person, try to create a team before confirming, confirm, create a team | Blocked until confirmed; then leader of an empty new team that can't see other teams | Blocked with "Please confirm your email address first."; after confirming, "You're its Team Leader." and no Brightside data | Pass |
| S12 | Sign up with Sarah's email (different capitals) | Refused | "An account with this email already exists."; still one Sarah | Pass |
| S13 | Sign up with password `password` | Refused, nothing saved | Back on the form; no person created | Pass |
| S14 | Forgot password for an unknown and a known email; use the reset link; reuse it | Same answer for both; new password works, old doesn't; link works once | All as expected | Pass |
| S15 | Member (Marcus) types `/Tasks/Create` or `/Projects/Create` | Not-allowed page | 403 "You don't have permission to do that" | Pass |
| S16 | Member posts create task, edit task, delete task, create project forms directly | All refused; nothing changes | 403 for all four; task count, title and projects unchanged | Pass |
| S17 | Member tries to make themself a manager, or to invite someone | Refused | 403 for both; no change, no invitation | Pass |
| S18 | Member comments on their task and picks up an unassigned task | Allowed | Comment saved; "You picked up this task." | Pass |
| S19 | Leader makes Priya a manager; Priya opens New task, then tries leader-only actions | New task allowed; leader actions refused | 200 for New task; 403 for permissions and invite | Pass |
| S20 | Leader gives Marcus manager permissions | Allowed | "Marcus Vance can now manage and approve tasks." | Pass |
| S21 | Sarah (Brightside) changes the number in the address to a Corner Bakery task and project, and posts status change and delete for it | Not found; nothing changes | 404 for the pages; "That task doesn't exist in this team." for the posts; task unchanged | Pass |
| S22 | Leo (Corner Bakery) downloads a Brightside file by number | Not found | 404 | Pass |
| S23 | Sarah edits her team cookie to Corner Bakery's team | Ignored | Still sees Brightside only | Pass |
| S24 | Marcus opens Sarah's notification by number | Not found; Sarah's stays unread | 404; unchanged | Pass |
| S25 | Member moves a teammate's task | Refused | "You can only update tasks assigned to you."; unchanged | Pass |
| S26 | Leader invites jamie@newhire.example; Leo tries to accept it; Jamie signs up, confirms and accepts | Only Jamie can accept, and joins as a member | 404 for Leo; Jamie joins as Team Member and gets 403 on New task | Pass |
| S27 | Live-site mode: open `/dev/mailbox`, read the sign-in page | Mailbox doesn't exist; no demo password shown | 404; no password or mailbox link | Pass |
| SQ | Start the app the way the live site does, on a new SQLite file | Tables created, sample data and accounts added, sign-in works, pages protected | All as expected | Pass |
| W1 | Member tries to approve a task (from Sprint 4) | Refused | "Only Team Leaders can mark tasks as Completed." | Pass |
| W2 | Post a form without the anti-forgery token (from Sprint 4) | Refused | 400 | Pass |

## 6. Known limits

- On the live site new sign-ups can't confirm their email until Atlas has an email service (DR-0013), so they can't
  create or join a team yet. The sample accounts are already confirmed.
- The sample accounts share one demo password (DR-0015). Fine for made-up data; to be retired before real use.
- SQLite on the live site is right for a class project, not for many simultaneous users (DR-0010).
- Not covered (the brief says they aren't expected yet): two-factor sign-in, penetration testing, rate limiting beyond
  account lockout, removing people from a team.
