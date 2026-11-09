# DR-0011 Sign in with ASP.NET Core Identity accounts linked to Atlas people

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** Assignment 6 asks for authentication and a trustworthy user identity. The Sprint 3 design calls for sign-up
with email confirmation and password reset (FT-01). Sprint 4 used a "Viewing as" switcher that let anyone act as anyone
(DR-0004). The Sprint 5 `Users` table is what tasks, comments and teams point to.

**Decision:** use ASP.NET Core Identity, the framework's built-in account system, rather than writing password handling
ourselves. Its accounts live in the Atlas database (`AspNetUsers` and three small tables; no site-wide role tables, as
roles belong to teams). Each account has a `UserId` pointing to one Atlas person, so the Sprint 5 tables didn't change.
After sign-in the person id travels in Identity's encrypted, signed cookie; `CurrentUserContext` reads it on every
request and is still the only class that decides who the current user is. Settings: passwords of 8+ characters with
upper case, lower case and a digit; lockout for 15 minutes after 5 wrong passwords; cookie `HttpOnly`, `Secure`,
`SameSite=Lax`, 8 hours sliding (or longer with "keep me signed in"). Every page requires sign-in by default (a fallback
policy); only sign-in, sign-up, password reset, error pages and `/health` are open. Sign-up refuses an email that
already belongs to an Atlas person, so nobody can claim a teammate's identity.

**Consequences:** passwords are stored only as salted hashes, and lockout, token links and cookie protection come from a
well-tested library. The demo switcher is removed. Supersedes DR-0004.
