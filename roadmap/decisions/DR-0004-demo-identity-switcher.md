# DR-0004 Stand in for sign-in with a "Viewing as" switcher

Date: 2026-10-25 · Status: superseded by DR-0011 · Plan: PLAN-001

**Context:** authentication is Sprint 6, but the leader-only completion gate and role permissions need a current user
to demonstrate.

**Decision:** a menu switches between the sample people, remembered in a cookie. `CurrentUserContext` is the only class
that reads it, and only accepts a team the person belongs to.

**Consequences:** anyone can act as anyone, which is fine for sample data only. Replaced by ASP.NET Core Identity in
PLAN-003.
