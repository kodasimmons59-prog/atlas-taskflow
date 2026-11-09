# DR-0014 Invite by email address, accepted inside Atlas

Date: 2026-11-09 · Status: accepted · Plan: PLAN-003

**Context:** US-03 and FT-02 have leaders invite people by email link. There is no email service yet (DR-0013).

**Decision:** a Team Leader enters an email address on the Team page, which saves a `TeamInvitation`. When someone
signs up or signs in with that email address, the invitation appears on their "My teams" page. Accepting needs a
confirmed email and only works for the account whose email matches; anyone else gets 404, even with the invitation's
number. Accepting adds them as a standard Team Member and removes the invitation. Anyone with a confirmed email can also
create a new team and becomes its leader (US-02).

**Consequences:** joining works with no email service and can't be hijacked by guessing ids. When email arrives, the
invitation can also be sent as a link without changing how it's accepted.
