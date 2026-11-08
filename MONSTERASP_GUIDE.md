# 🌐 MonsterASP.net Database Hosting Guide

## About MonsterASP.net

**MonsterASP.net** is a specialized cloud hosting provider for ASP.NET and .NET applications with integrated MSSQL databases.

- **Company:** MonsterASP.net
- **Specialization:** ASP.NET & .NET Cloud Hosting
- **Database Provider:** MSSQL (SQL Server)
- **Remote Access:** ✅ Supported via SSMS
- **Portal:** https://www.monsterasp.net/

---

## Your Databases on MonsterASP

### Database 1: DB_MasterERP (db67905)
- **Server:** db67905.public.databaseeasp.net
- **Port:** 1433
- **Login:** db67905
- **Database:** db67905
- **Purpose:** Master company data, platform users, system configuration
- **Status:** Empty (needs migrations)

### Database 2: tenant_a (db67904)
- **Server:** db67904.public.databaseeasp.net
- **Port:** 1433
- **Login:** db67904
- **Database:** db67904
- **Purpose:** Tenant-specific data (customers, bookings, payments)
- **Status:** Empty (needs migrations)

---

## MonsterASP Control Panel Access

### Login
1. Go to https://www.monsterasp.net/
2. Click "My Services" or "Client Login"
3. Enter your account credentials
4. Navigate to "My Database" or "Database Management"

### Managing Databases

**View Database Details:**
- Server name
- Database name
- Login credentials
- Connection strings
- Remote SSMS access details
- Database size/usage

**Common Operations:**
- Reset database password
- View remote access details
- Configure firewall IP whitelist
- Backup database
- Restore database
- Check database size limits

---

## Remote SSMS Access from MonsterASP

### Step 1: Get Connection Details
1. Login to MonsterASP control panel
2. Find your database (db67905 or db67904)
3. Click "Remote SSMS Access" tab
4. Note the connection details:
   - Server
   - Port
   - Login
   - Password (masked)

### Step 2: Connect via SQL Server Management Studio

**Open SSMS and:**
1. **Server name:** `db67905.public.databaseeasp.net,1433`
2. **Authentication:** SQL Server Authentication
3. **Login:** `db67905`
4. **Password:** [Your MonsterASP database password]
5. **Options > Connection Properties:**
   - Encryption: Required
   - TrustServerCertificate: Yes

### Step 3: Verify Connection
- Expand **Databases** to see your databases
- Should show empty databases initially
- After running migrations, you'll see tables

---

## Firewall Configuration

MonsterASP restricts database access by IP address for security.

### Check Your IP
1. Visit https://www.monsterasp.net/
2. Look for "Remote Access" section
3. See which IPs are whitelisted

### Add Your IP to Whitelist
1. Login to MonsterASP control panel
2. Find database in management section
3. Look for "Firewall" or "IP Whitelist" settings
4. Add your current IP address
5. Save changes

### If Connection Fails
- Check if your IP is in the whitelist
- Your ISP may provide dynamic IP (changes daily)
- Contact MonsterASP support to check current IP
- You may need to add a range of IPs if using dynamic IP

### Check Your Current IP
- Windows Command Prompt: `ipconfig /all` (look for IPv4 Address)
- Online: Visit https://www.whatismyip.com/
- PowerShell: `(Invoke-WebRequest ifconfig.me).Content`

---

## Connection Strings for SNAP

### Master ERP (db67905)
```
Server=db67905.public.databaseeasp.net,1433;
Database=db67905;
User Id=db67905;
Password=<MASTER_DB_PASSWORD>;
Encrypt=True;
TrustServerCertificate=True;
MultipleActiveResultSets=True;
```

### Tenant ERP (db67904)
```
Server=db67904.public.databaseeasp.net,1433;
Database=db67904;
User Id=db67904;
Password=<TENANT_DB_PASSWORD>;
Encrypt=True;
TrustServerCertificate=True;
MultipleActiveResultSets=True;
```

**These are configured in:**
- `SNAP/AppConfig.cs` (WinForms)
- `SNAP.api/appsettings.json` (ASP.NET API)

---

## Applying Migrations to MonsterASP Databases

### Prerequisites
- .NET SDK installed: `dotnet --version`
- Visual Studio or terminal access to project
- Your IP whitelisted in MonsterASP firewall
- Connection strings correct in AppConfig.cs

### Run Migrations

**Option 1: PowerShell Script (Recommended)**
```powershell
cd C:\Users\Admin\source\repos\SNAP\
powershell -ExecutionPolicy Bypass -File migrate-databases.ps1
```

**Option 2: Manual Command**
```bash
# Master DB migration
dotnet ef database update --context MasterErpDbContext --startup-project SNAP\SNAP.winforms.csproj

# Tenant DB migration  
dotnet ef database update --context TenantErpDbContext --startup-project SNAP\SNAP.winforms.csproj
```

**Option 3: Application Startup**
- Just run the SNAP application
- Migrations will apply automatically on first run

### Verify Migrations Worked

**Via SSMS:**
1. Connect to db67905 or db67904
2. Expand **Tables**
3. Should see: Companies, CompanyDatabases, PlatformUsers, etc.
4. Should NOT see just `__EFMigrationsHistory`

**Via PowerShell:**
```sql
USE db67905;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' 
ORDER BY TABLE_NAME;
```

---

## Troubleshooting MonsterASP Connection Issues

### "Connection Timeout" or "Cannot connect"
**Cause:** IP not whitelisted in MonsterASP firewall
**Solution:**
1. Check your current IP (https://www.whatismyip.com/)
2. Login to MonsterASP control panel
3. Add IP to database firewall whitelist
4. Wait 1-2 minutes for firewall to update
5. Try connecting again

### "Login failed for user 'db67905'"
**Cause:** Wrong password in connection string
**Solution:**
1. Login to MonsterASP control panel
2. Find database details
3. Copy exact password (watch for special characters)
4. Update AppConfig.cs and appsettings.json
5. Passwords are case-sensitive

### "Cannot open database 'db67905'"
**Cause:** Database doesn't exist or name is wrong
**Solution:**
1. Verify database names in MonsterASP: db67905, db67904
2. Check AppConfig.cs uses correct database names
3. Verify databases are "Enabled" in MonsterASP control panel

### "Encryption not supported"
**Cause:** Connection string missing encryption settings
**Solution:**
- Ensure all connection strings have:
  ```
  Encrypt=True;
  TrustServerCertificate=True;
  ```

### Migrations fail with "Object reference not set"
**Cause:** Migrations trying to apply to wrong database or context
**Solution:**
```bash
# Be explicit about context
dotnet ef database update --context MasterErpDbContext --verbose
```

---

## MonsterASP Support

### Contact Support
- **Website:** https://www.monsterasp.net/
- **Knowledgebase:** Available in control panel
- **Email Support:** Available through control panel
- **Live Chat:** Check main website

### Common Support Requests
- Reset database password
- Configure IP whitelist
- Check database size/usage
- Enable/disable remote SSMS access
- Restore from backup
- Database performance issues

---

## Best Practices for MonsterASP

1. **Regular Backups**
   - MonsterASP provides backup tools
   - Create backups before major changes
   - Store backup copies locally

2. **Monitor Database Size**
   - Check monthly usage in control panel
   - Monitor growth to avoid storage limits
   - Clean up old/unnecessary data regularly

3. **IP Whitelist Management**
   - Keep whitelist minimal for security
   - Remove old IPs you no longer use
   - If using dynamic IP, add entire range

4. **Password Security**
   - Change passwords periodically
   - Use strong passwords (MonsterASP generates them)
   - Never commit passwords to Git

5. **Connection String Security**
   - Store in configuration files not in code
   - Use appsettings.Development.json locally
   - Use environment variables in production
   - Add *.json to .gitignore

6. **Monitoring**
   - Regular test connections from client code
   - Monitor connection timeouts
   - Check error logs in application
   - Alert on failed migrations

---

## Connection Testing Script

**Test your MonsterASP connection:**
```csharp
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

class Program
{
	static void Main()
	{
		// Test Master DB
		TestConnection("db67905.public.databaseeasp.net", "db67905", "db67905", "<MASTER_DB_PASSWORD>");

		// Test Tenant DB
		TestConnection("db67904.public.databaseeasp.net", "db67904", "db67904", "<TENANT_DB_PASSWORD>");
	}

	static void TestConnection(string server, string database, string login, string password)
	{
		try
		{
			string connectionString = $"Server={server},1433;Database={database};User Id={login};Password={password};Encrypt=True;TrustServerCertificate=True;";

			ServerConnection connection = new ServerConnection(server)
			{
				LoginSecure = false,
				Login = login,
				Password = password
			};

			Server sqlServer = new Server(connection);
			Console.WriteLine($"✓ Connected to {server} successfully!");
			Console.WriteLine($"  Version: {sqlServer.Information.Version}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"✗ Failed to connect to {server}: {ex.Message}");
		}
	}
}
```

---

## Next Steps

1. ✓ Verify connection strings in AppConfig.cs
2. ✓ Confirm your IP is whitelisted in MonsterASP
3. ✓ Apply migrations (use migrate-databases.ps1)
4. ✓ Verify tables via SSMS
5. ✓ Launch SNAP application
6. ✓ Login and test functionality

Your SNAP ERP system is now ready to work with MonsterASP.net hosted databases! 🚀
