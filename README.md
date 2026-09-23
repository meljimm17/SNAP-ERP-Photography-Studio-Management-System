# SNAP ERP

Multi-tenant photography-studio ERP. WinForms client (`SNAP/`), domain model (`SNAP.domain/`),
data/services (`SNAP.infrastructure/`), and a minimal Web API (`SNAP.api/`).

## Local setup — database credentials

No connection string or password is committed to source. All database access is resolved by
`SNAP.infrastructure.config.ConnectionStrings`, which reads exclusively from environment variables.
If the variables below are not set, the app fails fast with a clear error message instead of
silently using a fake or stale credential.

Set these **before** running the WinForms app, the API, or any `dotnet ef` command:

| Variable | Purpose |
|---|---|
| `SNAP_MASTERERP_CONNECTIONSTRING` | Full connection string for the Master ERP database (db67905). Overrides the two below if set. |
| `SNAP_MASTERERP_PASSWORD` | Password for the Master ERP database, used only if `SNAP_MASTERERP_CONNECTIONSTRING` is not set. |
| `SNAP_TENANTERP_CONNECTIONSTRING` | Full connection string for the default/dev Tenant ERP database (db67904). Overrides `SNAP_TENANTDB_PASSWORD` if set. |
| `SNAP_TENANTDB_PASSWORD` | Password shared by tenant databases, used to build tenant connection strings (including the default dev tenant) when a full connection string isn't supplied. |
| `SNAP_TENANTERP_REMOTE_CONNECTIONSTRING` | Optional. Full connection string for a remote tenant database (e.g. the MonsterASP-hosted `db67904`) to background-sync to after every local save. If unset, sync is disabled entirely and the app behaves exactly as if this feature didn't exist — all writes stay local-only. See "Local-first sync to a remote database" below. |
| `SNAP_TENANTDB_PASSWORD_<KEY>` / `SNAP_TENANTERP_CONNECTIONSTRING_<KEY>` | Optional. Per-tenant override, used only for companies whose `CompanyDatabases.CredentialKey` is set. `<KEY>` is that company's `CredentialKey`, upper-cased with `-` replaced by `_` (e.g. `CredentialKey = "tenant_a"` reads `SNAP_TENANTDB_PASSWORD_TENANT_A`). Companies with no `CredentialKey` keep using the shared `SNAP_TENANTDB_PASSWORD` / `SNAP_TENANTERP_CONNECTIONSTRING` above. |

### PowerShell (current session only)

```powershell
$env:SNAP_MASTERERP_CONNECTIONSTRING = "Server=tcp:db67905.public.databaseeasp.net,1433;Database=db67905;User Id=db67905;Password=<your-password>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$env:SNAP_TENANTERP_CONNECTIONSTRING = "Server=tcp:db67904.public.databaseeasp.net,1433;Database=db67904;User Id=db67904;Password=<your-password>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
```

### PowerShell (persisted for your user account)

```powershell
[System.Environment]::SetEnvironmentVariable("SNAP_MASTERERP_CONNECTIONSTRING", "<connection string>", "User")
[System.Environment]::SetEnvironmentVariable("SNAP_TENANTERP_CONNECTIONSTRING", "<connection string>", "User")
```

Restart your terminal / Visual Studio after setting persisted variables.

### SNAP.api alternative: .NET user-secrets

From `SNAP.api/`:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:MasterErp" "<connection string>"
dotnet user-secrets set "ConnectionStrings:TenantErp" "<connection string>"
```

`SNAP.api/appsettings.json` only ships empty placeholders for these keys — never put a real
password there.

## Demo / offline login

If you don't have database access configured, `LoginForm` still supports fixed demo
credentials that never touch the database (company code `demo`):

| Username | Password | Role |
|---|---|---|
| `admin` | `admin123` | Admin |
| `staff` | `staff123` | Staff |
| `photographer` | `photographer123` | Photographer |

System admin demo login: `superadmin` / `superadmin123`.

Modules that read/write data (Bookings, Payments, Customers, etc.) still require a working
database connection even when logged in via demo credentials.

## Local-first sync to a remote database

Every save (Packages, Customers, Bookings, Payments, Tenant Users) always writes to whichever
database `SNAP_TENANTERP_CONNECTIONSTRING` points to first - this never changes. If
`SNAP_TENANTERP_REMOTE_CONNECTIONSTRING` is also set, `TenantSyncService`
(`SNAP.infrastructure/services/TenantSyncService.cs`) then best-effort pushes that same row to the
remote database in the background.

- If the remote is unreachable (offline, DNS, timeout), the row is simply left pending in the
  local `SyncOutboxEntries` table and retried automatically on the next save and the next login -
  no data is lost, and no save is ever blocked or slowed down waiting on the remote.
- The remote is only ever written to by this sync path. Don't edit data directly on the remote
  database while this is enabled - it isn't a two-way sync, and direct remote edits will be
  silently overwritten the next time the corresponding local row syncs.
- Leave `SNAP_TENANTERP_REMOTE_CONNECTIONSTRING` unset to disable this feature entirely; the app
  behaves exactly as it does without it.

## Running EF Core migrations

```powershell
cd SNAP.infrastructure
dotnet ef database update --context MasterErpDbContext --startup-project ../SNAP
dotnet ef database update --context TenantErpDbContext --startup-project ../SNAP
```

Requires the environment variables above to be set.
