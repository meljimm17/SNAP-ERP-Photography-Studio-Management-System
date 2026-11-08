# SNAP-ERP-Photography-Studio-Management-System

**SNAP ERP** is a robust, multi-tenant Enterprise Resource Planning (ERP) platform tailored for photography studios and creative agencies. Built on **.NET 10**, it combines a desktop Windows Forms client with a secure ASP.NET Core Web API and SQL Server backend.

---

### 🚀 Key Features

- **Multi-Tenant Architecture:** Self-healing schema migrations, tenant database isolation via JWT tokens, and multi-database routing.
- **Branch Scoping & Role-Based Access Control:** Fine-grained permissions by role (Admin, Branch Manager, Staff, Photographer) and branch isolation.
- **Booking & Scheduling:** Client appointments, packages, add-ons, and session tracking.
- **Inventory & Equipment Tracking:** Stock management, low-stock alerts, purchase order workflows, and studio equipment logging.
- **Point of Sale (POS) & Payments:** Receipts, billing, payment processing, and customer history.
- **Financial Management & Analytics:** Expense categorization, payroll/salary tracking, owner equity transactions, and BI charts (Revenue vs. Expenses, Branch Performance).
- **Master Admin Panel:** Company provisioning, subscription plan tier management (Micro, Small, Medium), and system audit logs.

---

### 🛠️ Tech Stack

- **Runtime:** .NET 10 (`net10.0-windows` & `net10.0`)
- **UI:** Windows Forms (Modern C# controls & Chart data visualization)
- **API:** ASP.NET Core Web API (JWT authentication, role/tier authorization attributes)
- **ORM & Database:** Entity Framework Core, SQL Server (Master & Tenant database separation)
- **Reporting:** QuestPDF & System.Windows.Forms.DataVisualization

---

## ⚙️ Local Setup — Database Credentials

No connection string or password is committed to source. All database access is resolved by `SNAP.infrastructure.config.ConnectionStrings`, which reads exclusively from environment variables. If the variables below are not set, the app fails fast with a clear error message instead of silently using a fake or stale credential.

Set these **before** running the WinForms app, the API, or any `dotnet ef` command:

| Variable | Purpose |
|---|---|
| `SNAP_MASTERERP_CONNECTIONSTRING` | Full connection string for the Master ERP database (db67905). Overrides the two below if set. |
| `SNAP_MASTERERP_PASSWORD` | Password for the Master ERP database, used only if `SNAP_MASTERERP_CONNECTIONSTRING` is not set. |
| `SNAP_TENANTERP_CONNECTIONSTRING` | Full connection string for the default/dev Tenant ERP database (db67904). Overrides `SNAP_TENANTDB_PASSWORD` if set. |
| `SNAP_TENANTDB_PASSWORD` | Password shared by tenant databases, used to build tenant connection strings (including the default dev tenant) when a full connection string isn't supplied. |
| `SNAP_TENANTERP_REMOTE_CONNECTIONSTRING` | Optional. Full connection string for a remote tenant database to background-sync to after every local save. If unset, sync is disabled entirely. |
| `SNAP_TENANTDB_PASSWORD_<KEY>` / `SNAP_TENANTERP_CONNECTIONSTRING_<KEY>` | Optional. Per-tenant override, used only for companies whose `CompanyDatabases.CredentialKey` is set. |

### PowerShell (Current Session Only)

```powershell
$env:SNAP_MASTERERP_CONNECTIONSTRING = "Server=tcp:<your-server>,1433;Database=<master-db>;User Id=<master-user>;Password=<your-password>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$env:SNAP_TENANTERP_CONNECTIONSTRING = "Server=tcp:<your-server>,1433;Database=<tenant-db>;User Id=<tenant-user>;Password=<your-password>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
```

### PowerShell (Persisted for User Account)

```powershell
[System.Environment]::SetEnvironmentVariable("SNAP_MASTERERP_CONNECTIONSTRING", "<connection string>", "User")
[System.Environment]::SetEnvironmentVariable("SNAP_TENANTERP_CONNECTIONSTRING", "<connection string>", "User")
```

---

## 👥 Demo / Offline Login

If you don't have database access configured, `LoginForm` still supports fixed demo credentials that never touch the database (company code `demo`):

| Username | Password | Role |
|---|---|---|
| `admin` | `admin123` | Admin |
| `staff` | `staff123` | Staff |
| `photographer` | `photographer123` | Photographer |

System admin demo login: `superadmin` / `superadmin123`.

---

## 🔄 Local-First Sync to Remote Database

Every save (Packages, Customers, Bookings, Payments, Tenant Users) always writes to whichever database `SNAP_TENANTERP_CONNECTIONSTRING` points to first. If `SNAP_TENANTERP_REMOTE_CONNECTIONSTRING` is also set, `TenantSyncService` best-effort pushes that same row to the remote database in the background.

---

## 🗄️ Running EF Core Migrations

```powershell
cd SNAP.infrastructure
dotnet ef database update --context MasterErpDbContext --startup-project ../SNAP
dotnet ef database update --context TenantErpDbContext --startup-project ../SNAP
```
