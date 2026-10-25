# Development log

Significant problems found while building Atlas, how they were investigated, and what fixed them. Sections 1 and 2 are
from Sprint 4; sections 3 to 5 are from Sprint 5 (persistent data).

## 1. Changing a task to an unknown status silently moved it to Not Assigned

**Found by:** the integration test `InvalidStatusValue_IsHandled`, which posts `status=42` to the change-status action.

**What happened:** the test expected the error "That isn't a valid status." Instead, the task's status became Not
Assigned and the page said "Moved to Not Assigned."

**Investigation:** the controller already checked `Enum.IsDefined(status)`, so the bad value should have been caught.
Printing the bound value showed it wasn't 42 at all; it was `NotAssigned`. ASP.NET Core's model binder refuses enum
values that aren't defined, records an error in `ModelState`, and leaves the parameter at its default. The default for
`TaskItemStatus` is its first value, `NotAssigned`, which *is* a defined value, so the check passed. For a leader, moving
to Not Assigned is allowed, so the task was unassigned.

**Fix:** the action now checks `ModelState.IsValid` as well as the value. Lesson: a failed bind doesn't throw, it
quietly hands you a default, so always check `ModelState` before trusting bound parameters.

## 2. The mobile menu button was invisible

**Found by:** viewing the dashboard at phone width in the browser walkthrough. The button took up space (Playwright
reported its position) but nothing was drawn.

**Investigation:** the project template ships Bootstrap 5.1. In 5.1 the menu icon only gets its image when the navbar
has `navbar-light` or `navbar-dark`. The views also used classes such as `text-body-secondary` and `text-bg-light` and
the button colour variables, which only exist from Bootstrap 5.3, so some styling was quietly missing too.

**Fix:** upgraded the bundled Bootstrap to 5.3.3. The icon, muted text and button colours all appeared.

## 3. `Task` clashes with .NET's `Task`

The design's entity is called Task, but every async controller action returns `System.Threading.Tasks.Task`. Using the
same name would mean writing the full namespace everywhere. The entity became `TaskItem` (and the status enum
`TaskItemStatus`).

## 4. Integration tests looked up the wrong team

The first run of the integration tests failed with "Sequence contains no matching element." The tests assumed the first
team had ID 1, but the in-memory store uses one ID counter for everything, so the four sample users take IDs 1 to 4 and
the first team is 5. The tests now look teams and people up by name instead of hard-coding IDs.

## 5. Cookies in integration tests

The demo user and team cookies are marked `Secure`, and browsers and the test client only send secure cookies back over
HTTPS. The test client's default address is `http://localhost`, so it was set to `https://localhost`; otherwise switching
users inside a test would not stick.

## 6. Small UI fixes from the walkthrough

- The fifth status column on project boards was cut off at desktop width; the minimum column width was reduced.
- "Switched to Corner Bakery Co.." had a doubled full stop because the team name ends in a period.
- The disabled team badge (shown for single-team users) picked up a dark border from Bootstrap's disabled style.

## 3. SQL Server refused to create the Notifications table

**Found by:** applying the first migration to SQL Server.

**What happened:** with the task link on Notifications set to "set to null when the task is deleted", the migration
failed: *Introducing FOREIGN KEY constraint 'FK_Notifications_Tasks_TaskId' on table 'Notifications' may cause cycles or
multiple cascade paths.*

**Investigation:** deleting a team already cascades to its notifications (Teams → Notifications). It also cascades
Teams → Projects → Tasks, and from Tasks a "set null" reaches Notifications again. SQL Server doesn't allow two automatic
paths into the same table, even when one of them only sets a value to null. ActivityEntries has the same shape.

**Fix:** the Tasks → Notifications and Tasks → ActivityEntries links are `NO ACTION` in the database
(`DeleteBehavior.ClientSetNull`), and `AtlasStore.DeleteTask` clears those `TaskId`s itself before removing the task, in
the same save. A test (`Delete_RemovesTaskWithItsCommentsAndFiles_KeepsNotifications`) checks that the notification
survives with no task link.

## 4. Tests passed against memory but read stale data against the database

**Found by:** running the Sprint 4 integration tests on the database version of the app.

**What happened:** `Leader_ApprovesTaskInReview` failed: *Expected: Completed, Actual: NeedsReview*, even though the page
said the task was approved.

**Investigation:** with the in-memory store, the test and the app shared the same task object, so the test saw the
app's change straight away. With the database, each web request has its own DbContext. The test was still holding the
copy of the task it loaded before the request, which never changes.

**Fix:** the tests now re-read from the database through a fresh DbContext after each action (`SavedStatus`, and a new
`Store` per use), so they check what was really saved. Lesson: once data lives in a database, an object in memory is
only a snapshot.

## 5. The "database unavailable" page needed the database

**Found by:** the test `DatabaseUnavailable_ShowsFriendlyPage_WithoutTechnicalDetails`, which starts the app pointed at
a database server that doesn't exist.

**What happened:** instead of the friendly page, the response was the raw `SqlException`: *A network-related or
instance-specific error occurred while establishing a connection to SQL Server.*

**Investigation:** the error page used the normal layout, and the layout reads the current user, the team list and the
unread-notification count from the database. So showing "can't reach the database" itself tried to reach the database
and failed. The old error action also lived in `HomeController`, whose constructor needs the database.

**Fix:** a separate `ErrorController` with no database dependencies, and a plain layout (`_PlainLayout`) for the error
pages. The test now gets a 503 with the friendly message and no technical details.

