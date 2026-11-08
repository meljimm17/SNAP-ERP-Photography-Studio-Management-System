# AGENTS.md — SNAP ERP

Multi-tenant photography-studio ERP. .NET 10, SQL Server, WinForms + JWT API.
Solution `SNAP.slnx`: `SNAP/` (WinForms `net10.0-windows`, entry `SNAP/Program.cs` → `LoginForm`), `SNAP.api/` (ASP.NET API), `SNAP.domain/` (entities/enums), `SNAP.infrastructure/` (EF contexts + services).

## Build / run

- Build: `dotnet build` (root). Run client: `dotnet run --project SNAP/SNAP.winforms.csproj`. API: `dotnet run --project SNAP.api` — serves `http://localhost:5254` (see `SNAP.api/Properties/launchSettings.json`). Kill stale API first if port is taken.
- No test projects, no lint/format/CI config. Verify with `dotnet build`.
- EF tooling: `Microsoft.EntityFrameworkCore.Design` is referenced by WinForms + API projects. Run migrations from `SNAP.infrastructure/`, e.g. `dotnet ef database update --context MasterErpDbContext --startup-project ../SNAP` (same for `TenantErpDbContext`). Or `powershell -ExecutionPolicy Bypass -File migrate-databases.ps1`. Migrations live in `SNAP.infrastructure/Migrations/` (`TenantErpDb/` subfolder for tenant).

## Config — never commit secrets

- `SNAP/appsettings.json` and `SNAP.api/appsettings.json` are gitignored; copy from `appsettings.example.json`. Never put real passwords in committed files (stale passwords in `QUICK_REFERENCE.md` / `CONNECTION_STRINGS_FIXED.md` are not to be reused or copied).
- Local dev uses `.\SNAPERP` with Integrated Security; `*_Remote` keys are MonsterASP hosts and inert unless code explicitly reads a `..._Remote` suffix.
- API key names: `ConnectionStrings:MasterErp`, `ConnectionStrings:TenantErp_TENANT_A/B/C` (no generic `TenantErp` key — Tenant A doubles as default/design-time tenant, see `ConnectionStrings.TenantErpDefault`). WinForms additionally reads `ApiBaseUrl` (defaults to `http://localhost:5254` in `SNAP/Shared/ApiConfig.cs`); its `ConnectionStrings` section is a temporary bridge for modules not yet migrated to the API — remove an entry when its module migrates.
- `Jwt:Key` rotation invalidates all tokens. WinForms `Program.cs` must keep QuestPDF `License = Community` + `UseSystemFonts = true` (Peso glyph) and the unhandled-exception → MessageBox handlers (WinForms on .NET Core otherwise dies silently).

## Architecture rules (enforced server-side, not in UI)

- Tenant resolution: `SNAP.api/Services/TenantDbContextFactory.cs` — tenant DB comes from the JWT `companyId` claim → `Companies`/`CompanyDatabases`, never client-supplied. It calls `Database.Migrate()` per request (self-healing schema).
- Branch isolation: `SNAP.api/Services/BranchScope.cs` — `branchId` claim locks Staff/Photographer/BranchManager to their branch; Admin/FinanceManager pass null = "All Branches". Controllers must call `ResolveForRead(branchId)` / `ResolveForWrite(db, request.BranchId)` — never trust a client-supplied branch. Client mirror is `SNAP/Shared/AppSession.cs` (`CurrentBranchId`, `IsBranchAware = tier >= Medium`).
- WinForms layout: one folder per module under `SNAP/` (`Booking/`, `Finance/`, …), shared client infra in `SNAP/Shared/` (`ApiClient`, `AppSession`, `AutoSyncService`). Per-module DTOs in `SNAP.domain/dtos/`.
- Tier + role gating: every API controller needs class-level `[RequireAccessLevel(Module, AssignedView)]` plus `[RequirePlanTier(...)]` where the module is tier-gated; each write method re-declares `[RequireAccessLevel(Module, Manage)]` (see `BookingController.cs`). Without these any authenticated role can call the endpoint — UI hiding is not enforcement. Permissions matrix lives in `SNAP.infrastructure/services/PermissionService.cs`.
- Branch scoping of entities: branch-scoped = Booking, Inventory, Equipment, Expense, PurchaseOrder (+lines via parent), ProductSale. Company-wide (no `BranchId`, never add one) = Customer, PhotographyPackage, Supplier, SalaryPayment, OwnerTransaction. `TenantUser.BranchId` stays nullable (null = company-wide roles); `AuditLog.BranchId` nullable, inherited.
- Master Admin (`CompaniesController`, SuperAdmin `CompanyId = 0` JWT) uses `MasterErpDbContext` directly — no `TenantDbContextFactory`.
- API JSON: `ReferenceHandler.IgnoreCycles` + `JsonStringEnumConverter` are load-bearing; `MapInboundClaims = false` keeps short claim names (`sub`, `companyId`, `branchId`). Error shape is always `{ "message": "..." }` — `SNAP/Shared/ApiClient.cs` depends on it.
- Charts: `System.Windows.Forms.DataVisualization` Chart control only, no third-party chart libs. Every colored bar needs a direct data label (status palette fails CVD separation).

## Migrations — safe backfill pattern

Adding a required FK to a table with rows: add column **nullable** → insert/backfill (e.g. one `IsMainBranch` row per tenant, point existing rows at it) → alter to `NOT NULL`, all in the same migration. Never add `NOT NULL` in one step. `seed-tenant-c.sql` is re-runnable (guarded MERGEs) — follow that idempotent style for seeds.

## Offline sync

Local-first: saves go to the local DB; `TenantSyncService` best-effort pushes to the tenant's `ConnectionStrings:{key}_Remote` entry (paired by DataSource+Catalog in `TryResolveRemoteForLocal`, not by name). No `_Remote` entry = sync disabled, local-only. WinForms `Shared/AutoSyncService.cs` retries on network-change + 5-second timer via `PushPendingIfIdle` — don't remove those triggers.
- Sync copies rows **by primary key** (IDENTITY_INSERT), so local and remote ids must stay in lockstep. Never write directly to a `_Remote` database (seeds, fixes, test rows) — write locally and queue `SyncOutboxEntries` (see end of `seed-demo-data.sql`). A remote with its own ids makes every later push that references them fail with FK errors.
- MonsterASP blocks a client IP for a while after a few failed logins. When scripting against it, strip `\r` from credentials read via sqlcmd/Windows tools, and force `tcp:host,1433`.

## Demo logins (offline, company code `demo`, no DB needed)

`admin`/`admin123`, `staff`/`staff123`, `photographer`/`photographer123`, sysadmin `superadmin`/`superadmin123`. Data modules still need a live DB.
