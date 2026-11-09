# Deploying Atlas TaskFlow to Azure

Atlas runs on **Azure App Service** (the hosting choice from the Sprint 3 architecture). Every push to `main` is built,
tested, deployed and smoke-tested by GitHub Actions (`.github/workflows/azure-deploy.yml`).

## One-time setup

You need an Azure account. [Azure for Students](https://azure.microsoft.com/free/students/) works and needs no card.

### 1. Create the App Service

1. In the [Azure portal](https://portal.azure.com), choose **Create a resource > Web App**.
2. Fill in:
   - **Resource group:** create one, e.g. `atlas-taskflow-rg`. If your school manages the subscription and you can't
     create one, choose **Select existing** and use the group you were given
   - **Name:** something unique, e.g. `atlas-taskflow-koda`. This becomes `https://<name>.azurewebsites.net`
   - **Publish:** Code
   - **Runtime stack:** .NET 8 (LTS)
   - **Operating system:** Linux
   - **Region:** one your subscription allows. Azure for Students only permits some regions; if you get "disallowed by
     Azure", try another (North Central US worked for this project), or check **Policy > Assignments > Allowed
     resource deployment regions** for the list
   - **Pricing plan:** Free F1 is enough for this sprint
3. Click **Review + create**, then **Create**.

> Keep the app at **one instance** (the default). The app updates the database schema when it starts, which should only
> happen from one place at a time (see DR-0008).

### 2. Let GitHub deploy to it

1. Open the new Web App. Go to **Settings > Configuration > General settings**, turn **SCM Basic Auth Publishing
   Credentials** **On**, and save. (Azure turns this off by default, and the publish profile needs it.)
2. On the **Overview** page click **Download publish profile**. This is a password for deploying, so don't commit it or
   paste it anywhere public.
3. In the GitHub repo go to **Settings > Secrets and variables > Actions**:
   - **Secrets** tab: **New repository secret**, name `AZURE_WEBAPP_PUBLISH_PROFILE`, value: the whole contents of the
     downloaded file.
   - **Variables** tab: **New repository variable**, name `AZURE_WEBAPP_NAME`, value: your app name (e.g. `atlas-taskflow-koda`).

### 3. The live database: SQLite (Sprint 6)

The live site keeps its data in a SQLite file on the App Service's own disk, `/home/data/atlas-taskflow.db`, which
survives restarts and deployments ([DR-0010](../roadmap/decisions/DR-0010-sqlite-on-live-site.md)). **There is nothing
to create in Azure for this**: `appsettings.json` already says `"Database": { "Provider": "Sqlite" }`, and the app
creates the file, the tables and the sample teams on its first start.

(Azure SQL is still possible later, see "Using Azure SQL instead" below, but Azure for Students refused to create one
in every region in Sprint 5.)

### 4. Set the demo password (Sprint 6)

The sample people (Sarah, Marcus, Priya, Leo) get sign-in accounts when the app starts, using a password from an App
Service setting, so the password isn't in the code on GitHub ([DR-0015](../roadmap/decisions/DR-0015-demo-accounts.md)).

1. Open the web app, then **Settings > Environment variables**, **App settings** tab, and click **Add**:
   - **Name:** `DemoAccounts__Password` (two underscores)
   - **Value:** a demo password you make up, with 8+ characters, an upper case letter, a lower case letter and a
     number. Use a different one from the PC's `AtlasDemo2026`, because that one is on GitHub for anyone to read. Put
     the live one in your submission so your instructor can sign in
2. Click **Apply**, then **Apply** again at the bottom, and confirm. The app restarts.

Without this setting the site still works, but nobody can sign in as the sample people (the log says
"DemoAccounts:Password is not set"). The setting is only used to create accounts that don't exist yet, so changing it
later doesn't change existing passwords.

### 5. Deploy

Push to `main`, or open **Actions > Build, test and deploy to Azure > Run workflow**. The run has two jobs:

- **build**: restore, build, run all tests, publish
- **deploy**: upload to App Service, then run the post-deployment smoke test against the live URL

Until the variable from step 2 exists, the deploy job is skipped and only build and tests run.

## Configuration notes

- The only runtime secret is the demo password (step 4); with Azure SQL, the connection string too. Both are App Service
  settings, never in the repo.
- On start the app applies any new migrations, adds the sample teams if the database is empty, and gives the sample
  people accounts if they don't have one. If the database can't be reached, the app still starts and pages show a
  friendly "Atlas can't reach its database right now" message.
- Everything except sign-in, sign-up, password reset, the error pages and `/health` needs a signed-in user.
- Account emails (confirmation, password reset) are not sent yet; on the live site they are only noted in the log
  ([DR-0013](../roadmap/decisions/DR-0013-email-to-log.md)).
- App Service sets `ASPNETCORE_ENVIRONMENT=Production` by default, which turns on HSTS and the friendly error page.
- `/health` returns `Healthy` only when the database answers too, and is used by the smoke test. You can also set it as the App Service health check path
  (**Monitoring > Health check**).
- Data is kept in `/home/data`, so it survives restarts, deployments and the Free tier idling out. Keep the app at one
  instance: SQLite is a single file.

## Checking persistence on the live site

1. Open the live site, sign in as Sarah, and create a task (or edit one) with a name you'll recognise.
2. In the portal, open the web app and click **Restart** on the Overview page. Wait for it to come back.
3. Reload the site and find the task. It should still be there with the same details.

## Using Azure SQL instead

If Azure for Students ever allows it (see DR-0009), the same code runs on Azure SQL:

1. **Create a resource > SQL Database**, in the same resource group and region as the web app, with a new server using
   **SQL authentication** (keep the admin password in a password manager). Under **Networking** allow Azure services to
   access the server.
2. Copy the database's **ADO.NET (SQL authentication)** connection string, and fill in the password.
3. In the web app's **Environment variables**: on **Connection strings** add `DefaultConnection` (type `SQLAzure`) with
   that string, and on **App settings** add `Database__Provider` = `SqlServer`. Apply. The app creates the tables on start.

## Evidence to collect for the assignment

- A screenshot of the green workflow run in the **Actions** tab, with the smoke test step expanded
- The live URL loading in a browser
- One workflow working live (create, submit, approve), see the checklist in [TESTING.md](TESTING.md)
