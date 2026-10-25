# Atlas TaskFlow

Atlas TaskFlow is a task manager for small business teams. Team leaders create and assign work and review it before it's
closed out; team members see what's on their plate, pick up open work, and submit deliverables. This repository is the
Sprint 5 build: the cloud-hosted app from Sprint 4, now storing its data in SQL Server through Entity Framework Core.

**Live app:** <https://atlas-taskflow-koda-d8gjabatgvatgtcj.northcentralus-01.azurewebsites.net>

## Try it

The app opens as **Sarah Chen**, the Team Leader persona. Use the name menu at the top right ("Viewing as") to switch to
**Marcus Vance**, a Team Member, and try the other side of the workflow. Marcus belongs to two businesses, so the team
switcher in the navigation bar lets him move between Brightside Marketing and Corner Bakery Co.

A quick walkthrough of the main workflow (Sprint 3, Journey 2):

1. As Sarah, click **+ New task**, fill in the form and assign it to Marcus.
2. Switch to Marcus. The bell shows a new notification and the task is on his dashboard.
3. Open the task, click **Start working**, attach a file and click **Submit files**. The task moves to **Needs Review**.
4. Try setting the status to **Completed** as Marcus. It's blocked: "Only Team Leaders can mark tasks as Completed."
5. Switch back to Sarah, open the notification, check the file and click **Approve and complete** (or **Request changes**).

Everything you create or change is saved in the database and is still there after the app restarts. The sample teams
are added once, the first time the app runs against an empty database.

## Run it locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and SQL Server LocalDB, which Visual Studio
installs with the "ASP.NET and web development" workload.

```bash
dotnet run --project src/AtlasTaskFlow      # creates the AtlasTaskFlow database on first run, then open the URL it prints
dotnet test                                 # unit and integration tests; each test uses its own throwaway database
```

In Visual Studio, set **AtlasTaskFlow** as the startup project and press F5. To look at the tables, open **View > SQL
Server Object Explorer** and expand `(localdb)\MSSQLLocalDB > Databases > AtlasTaskFlow > Tables`.

The connection string is `DefaultConnection` in `src/AtlasTaskFlow/appsettings.Development.json`. To run the tests
against another SQL Server, set `ATLAS_TEST_SQLSERVER` to a connection string without a database name.

Schema changes use EF Core migrations:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/AtlasTaskFlow --output-dir Data/Migrations
```

The app applies new migrations itself when it starts.

## How it's built

ASP.NET Core 8 MVC, following the course pattern of models, controllers and Razor views with a shared layout.

```
src/AtlasTaskFlow/
  Models/        Domain classes from the Sprint 3 data design, plus view models for the pages
  Data/          AtlasDbContext (EF Core mapping), Migrations/, and database error handling
  Services/      AtlasStore (reads and writes through EF Core), TaskWorkflowService (business rules),
                 CurrentUserContext (who is using the app), SeedData (sample teams and tasks)
  Controllers/   Home (dashboard), Tasks, Projects, Team, Notifications, Demo (user/team switcher), Error
  Views/         Razor pages and the shared layout
  wwwroot/       CSS, the polling script and Bootstrap 5.3
tests/AtlasTaskFlow.Tests/
  TaskWorkflowServiceTests.cs   Unit tests for every workflow rule
  AppIntegrationTests.cs        Runs the whole app in memory and drives it over HTTP
  DataTests.cs                  CRUD, validation, database errors and the restart test
  TestDatabase.cs               Creates and drops a SQL Server database per test
.github/workflows/azure-deploy.yml   Build, test, deploy to Azure App Service, then smoke test
```

The rules live in `TaskWorkflowService`, not in controllers or views. Controllers translate web requests into calls on
that service and pick a page to show. That kept the rules easy to unit test and means the same checks apply no matter
which page or button triggers them.

## How it maps to the Sprint 3 design

| Design item | Where it is |
| --- | --- |
| FR-2.1 Default "Tasks" project per team | `AtlasStore.CreateTeam`; new tasks default to it (US-05) |
| FR-2.2 Five statuses | `TaskItemStatus`; columns on each project board |
| FR-2.3 Zero, one or many assignees | Assign-to checkboxes on the task form |
| FR-3.1 / 3.2 Leaders manage tasks, members update their own | `TaskWorkflowService.CheckStatusChange` and the create/edit/delete checks |
| FR-3.3 / US-09 Only leaders can mark Completed | Same, with the exact message from the acceptance criteria |
| US-07 Not Assigned pool | Tasks > Not assigned, and **Pick up this task** |
| US-08 Submit Files moves the task to Needs Review and notifies the leader | **Submit files** on the task page |
| US-10 Discussion thread | Discussion section on each task |
| FR-4.1 / US-11 Dashboard with tasks, projects, calendar | Dashboard (leaders also get a review queue) |
| FR-4.2 Bell notifications on assignment and review | Bell in the top bar and the Notifications page |
| FR-4.3 Updates without a manual refresh | Bell and My Tasks panel refresh every 15 seconds |
| US-04 Switch teams without logging out | Team switcher in the top bar; data is filtered by the active team |
| FT-10 Leader delegates manager permissions | Team page, **Make manager** |

### What's deliberately not in this sprint

The assignment defers these to later sprints, so they are stubbed or left out:

- **Sign-up, login, email verification, password reset and invitations** (FT-01, FT-02 invites): Sprint 6. A "Viewing as"
  switcher stands in for login so both roles can be demonstrated. Only `CurrentUserContext` needs to change when real
  authentication is added.
- **SignalR**: polling is used instead (see below).

### Where the implementation differs from the design

- **Polling instead of SignalR for live updates.** The design listed polling as the fallback if WebSockets were
  unreliable. On a single server, polling every 15 seconds meets FR-4.3 with far fewer moving parts.
- **`TaskItem` instead of `Task`.** `Task` collides with .NET's `System.Threading.Tasks.Task`, which every async
  controller uses, so the entity was renamed.
- **Recent activity feed.** The dashboard wireframe showed recent activity but the data design had no table for it, so an
  `ActivityEntry` record (the ActivityEntries table) was added.
- **Files in the database, not Blob Storage.** Koda's Sprint 5 choice; see
  [DR-0007](roadmap/decisions/DR-0007-files-in-database.md).
- **TaskAssignees table.** The design's TaskAssignee junction is a real table with a (TaskId, UserId) key.
- **Request changes is its own button.** The design described the leader commenting and then moving the task back. The
  app does both in one step and requires feedback, so a task is never sent back without an explanation.

## Docs

- [Data model](docs/DATA-MODEL.md): the tables, keys, relationships and constraints
- [Roadmap](roadmap/ROADMAP.md): what's planned, what state it's in, decisions and Koda's rulings
- [Deployment](docs/DEPLOYMENT.md): Azure App Service and Azure SQL setup
- [Testing evidence](docs/TESTING.md): what was tested before and after deployment, with results
- [Development log](docs/DEV-LOG.md): problems hit during the build and how they were solved
