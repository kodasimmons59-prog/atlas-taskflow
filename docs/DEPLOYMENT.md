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

### 3. Create the Azure SQL database (Sprint 5)

1. **Create a resource > SQL Database**. If the portal offers **Apply offer** for the free Azure SQL Database, use it.
2. Fill in:
   - **Resource group:** the same one as the web app
   - **Database name:** `atlas-taskflow-db`
   - **Server:** **Create new**, e.g. `atlas-taskflow-sql-koda`, in the same region as the web app, with **Use SQL
     authentication**. Keep the admin password in a password manager; it never goes in the repo or in chat
   - **Behavior when free limit reached:** **Auto-pause the database until next month**, so it can't cost anything
3. **Networking:** Public endpoint, **Allow Azure services and resources to access this server: Yes** (so the web app can
   connect) and **Add current client IP address: Yes** (so you can look at the tables from your PC or the portal).
4. **Review + create**, then **Create**. The tables don't need creating by hand: the app does it on first start.

### 4. Give the web app the connection string

1. Open the database, then **Settings > Connection strings**, and copy the **ADO.NET (SQL authentication)** string.
2. Open the web app, then **Settings > Environment variables > Connection strings** tab, and click **Add**:
   - **Name:** `DefaultConnection`
   - **Value:** the copied string, with `{your_password}` replaced by the admin password
   - **Type:** `SQLAzure`
3. Click **Apply**, then **Apply** again at the bottom, and confirm. The app restarts, creates the tables and adds the
   sample teams.

App Service hands this to the app as `ConnectionStrings:DefaultConnection`, the same name `appsettings.Development.json`
uses for LocalDB, so the code is identical in both places and the password only exists in Azure.

### 5. Deploy

Push to `main`, or open **Actions > Build, test and deploy to Azure > Run workflow**. The run has two jobs:

- **build**: restore, build, run all tests, publish
- **deploy**: upload to App Service, then run the post-deployment smoke test against the live URL

Until the variable from step 2 exists, the deploy job is skipped and only build and tests run.

## Configuration notes

- The only runtime secret is the database connection string, kept as an App Service connection string setting (step 4).
- On start the app applies any new migrations and, if the database is empty, adds the sample teams. If the database
  can't be reached, the app still starts and pages show a friendly "Atlas can't reach its database right now" message.
- App Service sets `ASPNETCORE_ENVIRONMENT=Production` by default, which turns on HSTS and the friendly error page.
- `/health` returns `Healthy` only when the database answers too, and is used by the smoke test. You can also set it as the App Service health check path
  (**Monitoring > Health check**).
- Data is kept in Azure SQL, so it survives restarts, deployments and the Free tier idling out. The free database
  auto-pauses when idle; the first request after that can take up to a minute while it resumes (the app retries).

## Checking persistence on the live site

1. Open the live site and create a task (or edit one) with a name you'll recognise.
2. In the portal, open the web app and click **Restart** on the Overview page. Wait for it to come back.
3. Reload the site and find the task. It should still be there with the same details.
4. Optional: in the database, open **Query editor**, sign in, and run
   `SELECT TaskId, Title, Status, DueDate FROM Tasks ORDER BY TaskId DESC` to see the row itself.

## Evidence to collect for the assignment

- A screenshot of the green workflow run in the **Actions** tab, with the smoke test step expanded
- The live URL loading in a browser
- One workflow working live (create, submit, approve), see the checklist in [TESTING.md](TESTING.md)
