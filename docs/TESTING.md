# Testing evidence

Sprint 5 (persistent data) is first; the Sprint 4 evidence it builds on follows from "Sprint 4" below.

# Sprint 5: persistent data

## 5.1 Automated tests against SQL Server

54 tests, all passing on 2026-10-25. Every test runs against a real SQL Server database: each test creates its own empty
database from the migrations, and drops it afterwards. Locally that's LocalDB; in GitHub Actions it's a SQL Server 2022
container. The 44 Sprint 4 tests all still pass on the database version, and 10 new tests (`DataTests.cs`) cover
persistence:

| Assignment test | Test | What it proves | Result |
| --- | --- | --- | --- |
| Create | `CreateTask_WithValidInput_AppearsOnBoard`, `CreateProject_IsSaved` | A new task and a new project are saved | Pass |
| Read | `Read_TaskDetailsShowSavedCommentAndFile` | A saved comment and file show on the task page, and the file downloads with the right contents | Pass |
| Update | `Update_EditTask_SavesNewValues` | New title, description, due date and assignees are saved; the old assignee is removed | Pass |
| Delete | `Delete_RemovesTaskWithItsCommentsAndFiles_KeepsNotifications` | The task, its comments and its files are gone; its notification stays without a link | Pass |
| Required field | `CreateTask_WithoutTitle_ShowsValidationMessage` | "Give the task a title." and nothing saved | Pass |
| Invalid input | `Update_TitleTooLong_IsRejectedAndNothingChanges`, `CreateTask_WithPastDueDate_IsRejected`, `CreateTask_WithGarbageDate_IsRejected`, `CreateProject_DuplicateName_ShowsValidationMessage` | Bad input is rejected with a message and the database is unchanged | Pass |
| Persistence after restart | `Persistence_TaskSurvivesAppRestart` | A task created through the app is still there, with the same details, after the app is stopped and a new one started on the same database; the sample data isn't added twice | Pass |
| Error handling | `Database_RejectsDuplicateEmail` | The database itself refuses a duplicate email (unique index) | Pass |
| Error handling | `RejectedSave_BecomesFriendlyMessage_OnThePageYouCameFrom` | A save the database rejects becomes a plain message on the previous page | Pass |
| Error handling | `DatabaseUnavailable_ShowsFriendlyPage_WithoutTechnicalDetails` | With no database, pages return 503 "Atlas can't reach its database right now", with no exception text; `/health` reports 503 | Pass |

## 5.2 Browser walkthrough and restart, local

On 2026-10-25 a Playwright script drove Chromium through the data lifecycle on a Release build in Production mode,
connected to SQL Server 2022, starting from an empty database. Screenshots are in [`screenshots/`](screenshots/):

| # | Step | Result | Screenshot |
| --- | --- | --- | --- |
| 1 | New task with no title | "Give the task a title." | `s5-01-validation-required.png` |
| 2 | New task due in the past | "The due date can't be in the past." | `s5-02-validation-past-date.png` |
| 3 | Fill in "Persistence check: client logo files", Website Refresh, Marcus | Form ready | `s5-03-create-form.png` |
| 4 | Create | "Created …" and the task page | `s5-04-created-details.png` |
| 5 | Edit the title and due date | "Task updated." with the new values | `s5-05-updated.png` |
| 6 | All team tasks before the restart | Task listed | `s5-06-before-restart.png` |
| 7 | Delete "Update team holiday calendar" | Confirmation page, then "Deleted …" | `s5-07-delete-confirm.png`, `s5-08-deleted.png` |
| 8 | **Stop the app** (process ended at 22:38:39 UTC), then start it again (22:38:46 UTC) | Started without re-adding sample data | (log) |
| 9 | All team tasks after the restart | Updated task still there; deleted task still gone | `s5-09-after-restart.png` |
| 10 | Open the task after the restart | Same title, description, project, due date and assignee | `s5-10-after-restart-details.png` |
| 11 | Start the app with an unreachable database | 503 "Atlas can't reach its database right now" | `s5-11-database-unavailable.png` |

The rows behind steps 4 to 9, queried in SQL Server after the restart:

| TaskId | Title | Status | DueDate | Project | Assigned to |
| --- | --- | --- | --- | --- | --- |
| 11 | Persistence check: client logo files (updated) | Assigned | 2026-11-07 | Website Refresh | Marcus Vance |

`SELECT COUNT(*) FROM Tasks WHERE Title = 'Update team holiday calendar'` returned 0, and `__EFMigrationsHistory`
holds `20261025222552_InitialCreate`.

## 5.3 Schema check

After the migration, SQL Server has the ten Atlas tables (ActivityEntries, Attachments, Comments, Notifications,
Projects, TaskAssignees, Tasks, TeamMembers, Teams, Users) plus `__EFMigrationsHistory`, and 17 foreign keys matching
[DATA-MODEL.md](DATA-MODEL.md). `dotnet ef migrations has-pending-model-changes` reports no differences between the
model and the migrations.

## 5.4 Live site (Azure SQL)

To be filled in after the Azure SQL deployment: create a task on the live site, restart the App Service from the
portal, and confirm the task is still there ([DEPLOYMENT.md](DEPLOYMENT.md), "Checking persistence on the live site").

# Sprint 4

Testing happened at three levels: automated unit and integration tests on every build, a scripted browser walkthrough
locally, and a smoke test against the live Azure site after each deployment.

## 1. Automated tests (`dotnet test`)

44 tests, all passing locally on 2026-10-25. They also run in GitHub Actions on every push and pull request; the results
are uploaded as the `test-results` artifact.

**Unit tests** (`TaskWorkflowServiceTests`, 20 tests) check each rule in isolation:

| Area | What's checked |
| --- | --- |
| Projects | New teams get a default "Tasks" project |
| Creating tasks | No assignees means Not Assigned; several assignees all get notified; members can't create; people outside the team are ignored |
| Completion gate | Members get "Only Team Leaders can mark tasks as Completed."; leaders and delegated managers can approve; the completion time is recorded |
| Permissions | Members can't update tasks that aren't theirs; tasks from another team can't be changed or deleted |
| Pool | Picking up assigns the task to you; a task someone already has can't be picked up |
| Submitting work | Files are saved and the task moves to Needs Review with the leader notified; no files, or a file over 5 MB, is rejected |
| Review | Request changes sends the task back to In Progress with the feedback as a comment; empty feedback is rejected |
| Editing | Removing every assignee returns the task to Not Assigned |
| Discussion | Empty comments are rejected |

**Integration tests** (`AppIntegrationTests`, 24 tests) start the real app in memory and use it over HTTP like a browser:

| Assignment test type | Test | Result |
| --- | --- | --- |
| Core navigation | Dashboard, every task tab, new task, projects, new project, team and notifications pages load (10 cases) | Pass |
| Core navigation | The dashboard shows the review queue, my tasks, projects and recent activity | Pass |
| Primary workflow | Leader creates a task and it is saved as Assigned | Pass |
| Primary workflow | Member submits files and the task moves to Needs Review with the file attached | Pass |
| Primary workflow | Leader approves a task in review | Pass |
| Primary workflow | Member trying to complete a task sees the leader-only message and the status doesn't change | Pass |
| Validation | Missing title shows "Give the task a title." and nothing is saved | Pass |
| Validation | A due date in the past is rejected | Pass |
| Unexpected input | A due date that isn't a date is rejected | Pass |
| Unexpected input | An unknown status value is rejected (this test found a real bug, see the dev log) | Pass |
| Unexpected input | A form posted without its anti-forgery token is refused (400) | Pass |
| Error handling | A task that doesn't exist shows the friendly 404 page | Pass |
| Data isolation | After switching teams, the other team's tasks are hidden and can't be opened by URL | Pass |
| Live updates | The notification count endpoint returns the unread count | Pass |
| Deployment | `/health` returns Healthy | Pass |

## 2. Local browser walkthrough

A Playwright script drove Chromium through the main journey on a local Release build running in Production mode.
Every step completed. Screenshots are in [`screenshots/`](screenshots/):

| # | Step | Screenshot |
| --- | --- | --- |
| 1 | Leader dashboard loads | `01-dashboard-leader.png` |
| 2 | Projects list | `02-projects.png` |
| 3 | Project status board with all five columns | `03-board.png` |
| 4 | Task waiting for review, with approve / request changes | `04-review-task.png` |
| 5 | Empty title shows a validation message | `05-validation.png` |
| 6 | Task created and assigned to Marcus | `06-created.png` |
| 7 | Switched to Marcus: new task on his dashboard, bell shows a notification | `07-dashboard-member.png` |
| 8 | Marcus starts work, uploads a file and submits: Needs Review | `08-submitted.png` |
| 9 | Marcus tries to mark it Completed and is blocked | `09-blocked.png` |
| 10 | Marcus switches to his other team | `10-team-switch.png` |
| 11 | Sarah sees the review notification | `11-notifications.png` |
| 12 | Sarah approves: Completed | `12-approved.png` |
| 13 | Unknown task shows the 404 page | `13-not-found.png` |
| 14 | Dashboard on a phone-sized screen (390 px) | `14-mobile-dashboard.png` |

The page had no JavaScript errors; the only console message was the expected 404 from step 13.

## 3. Post-deployment smoke test

The deploy job checks the live site automatically after every deployment:

| Check | Expected |
| --- | --- |
| `/health` | 200, "Healthy" |
| `/` (dashboard) | 200, page contains "Atlas TaskFlow" |
| `/Tasks` | 200 |
| `/Projects` | 200, shows "Spring Campaign Launch" |
| `/Team` | 200, shows "Brightside Marketing" |
| `/Tasks/Details/999999` | 404 |

**First deployment, 2026-10-25** ([workflow run](https://github.com/kodasimmons59-prog/atlas-taskflow/actions/runs/40232502012), commit `d1ba462`), against <https://atlas-taskflow-koda-d8gjabatgvatgtcj.northcentralus-01.azurewebsites.net>:

| Check | Result |
| --- | --- |
| `/health` | 200 Healthy |
| `/` | 200 |
| `/Tasks` | 200 |
| `/Projects` | 200 |
| `/Team` | 200 |
| `/Tasks/Details/999999` | 404 as expected |

**Manual checks on the live site** (fill these in yourself in a browser):

| Check | Result | Date |
| --- | --- | --- |
| Live URL loads over HTTPS |Pass |10/25/26 |
| Create a task as Sarah and assign it to Marcus |Pass |10/25/26 |
| As Marcus: start, upload a file, submit for review |Pass |10/25/26 |
| As Marcus: try to mark Completed and see the block message |Pass |10/25/26 |
| As Sarah: open the notification and approve |Pass |10/25/26 |
| Switch Marcus to Corner Bakery Co. and confirm only its tasks show |Pass |10/25/26 |
| Open the site on a phone |Fail |10/25/26 |
