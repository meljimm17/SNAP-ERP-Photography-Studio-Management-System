# ⚡ Quick Reference - SNAP ERP Database Setup

## 🌐 Hosting Provider
**MonsterASP.net** (NOT Azure SQL)

---

## 📊 Your Databases

| Database | Server | Port | Login | Purpose |
|----------|--------|------|-------|---------|
| **db67905** | db67905.public.databaseeasp.net | 1433 | db67905 | Master ERP |
| **db67904** | db67904.public.databaseeasp.net | 1433 | db67904 | Tenant (tenant_a) |

---

## 🔑 Connection Strings

### Master ERP
```
Server=db67905.public.databaseeasp.net,1433;Database=db67905;User Id=db67905;Password=rq+3ATs+95Lg+;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

### Tenant ERP
```
Server=db67904.public.databaseeasp.net,1433;Database=db67904;User Id=db67904;Password=D17-2F!sal4lQ;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

---

## 🚀 Quick Start

### 1. Apply Migrations
```powershell
cd C:\Users\Admin\source\repos\SNAP\
powershell -ExecutionPolicy Bypass -File migrate-databases.ps1
```

### 2. Test Connection in SSMS
- **Server:** `db67905.public.databaseeasp.net,1433`
- **Auth:** SQL Server Authentication
- **Login:** `db67905`
- **Password:** `rq+3ATs+95Lg+`

### 3. Run Application
```bash
dotnet run --project SNAP\SNAP.winforms.csproj
```

### 4. Login
- **Company Code:** `demo` or `tenant_a`
- **Username:** `admin`
- **Password:** `admin123`

---

## ✅ Verification Checklist

- [ ] MonsterASP databases exist (db67905, db67904)
- [ ] Your IP is whitelisted in MonsterASP firewall
- [ ] Connection strings match passwords exactly
- [ ] SSMS can connect to both databases
- [ ] Migrations have been applied
- [ ] Tables exist in both databases
- [ ] SNAP application starts without errors
- [ ] Demo login works

---

## 🔧 Configuration Files

| File | Setting | Value |
|------|---------|-------|
| `SNAP/AppConfig.cs` | MasterErpConnectionString | db67905 connection |
| `SNAP/AppConfig.cs` | TenantErpConnectionString | db67904 connection |
| `SNAP.api/appsettings.json` | MasterErp | db67905 connection |
| `SNAP.api/appsettings.json` | TenantErp | db67904 connection |

---

## 🆘 Common Issues

| Problem | Solution |
|---------|----------|
| Connection timeout | Whitelist your IP in MonsterASP firewall |
| Login failed | Check password matches exactly (special chars) |
| No tables | Run migrations: `migrate-databases.ps1` |
| SSMS can't connect | Verify encryption settings: `Encrypt=True;TrustServerCertificate=True;` |
| Migration errors | Add `--verbose` flag: `dotnet ef database update --verbose` |

---

## 📞 Resources

- **MonsterASP Portal:** https://www.monsterasp.net/
- **Database Guide:** See `MONSTERASP_GUIDE.md`
- **Setup Guide:** See `DATABASE_SETUP.md`
- **Connection Strings:** See `CONNECTION_STRINGS_FIXED.md`

---

## 🎯 Next Steps

1. Ensure migrations are applied
2. Verify database tables exist
3. Run SNAP application
4. Test with demo credentials
5. Create your own company and users

**Status: Ready to Deploy! ✅**
