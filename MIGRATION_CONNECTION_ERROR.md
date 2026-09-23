# ⚠️ Migration Connection Error - Diagnostic Guide

## Error Details
```
Error Number: 11001
Message: Host not found (DNS resolution failed)
Server: db67905.public.databaseeasp.net
```

---

## 🔍 Possible Causes

### 1. ❌ IP Not Whitelisted in MonsterASP Firewall (MOST LIKELY)
MonsterASP restricts database access by IP address for security. Your current IP may not be whitelisted.

**Solution:**
1. Find your current IP address
2. Login to MonsterASP control panel
3. Add your IP to the database firewall whitelist
4. Wait 1-2 minutes for firewall to update
5. Retry migration

### 2. ❌ Dynamic IP Address
Your Internet Service Provider may assign dynamic IPs that change daily or when reconnecting.

**Solution:**
- Check if your IP has changed: https://www.whatismyip.com/
- Update MonsterASP firewall whitelist if changed
- Add a wider IP range if using dynamic IP

### 3. ❌ Network Connectivity Issues
Local firewall or network proxy blocking the connection.

**Solution:**
- Check Windows Firewall isn't blocking SQL connections
- Test from command line: `ping db67905.public.databaseeasp.net`
- Try SSMS connection first (to test connectivity)

### 4. ❌ DNS Resolution Problem
Your DNS cannot resolve the MonsterASP domain name.

**Solution:**
- Try pinging the server: `ping db67905.public.databaseeasp.net`
- Try using IP address instead (if you have it)
- Check your DNS settings

---

## ✅ Step-by-Step Fix

### Step 1: Find Your IP Address

**Option A: Online**
```
Visit: https://www.whatismyip.com/
Note the IPv4 address shown
```

**Option B: PowerShell**
```powershell
(Invoke-WebRequest ifconfig.me).Content
```

**Option C: Command Prompt**
```cmd
ipconfig /all
Look for "IPv4 Address" under your network adapter
```

**My IP is: ___________________** (Write it here)

---

### Step 2: Test Connectivity to MonsterASP

**In PowerShell, run:**
```powershell
# Test DNS resolution
Resolve-DnsName db67905.public.databaseeasp.net

# Or test ping
ping db67905.public.databaseeasp.net

# Or test connection to port 1433
Test-NetConnection -ComputerName db67905.public.databaseeasp.net -Port 1433
```

**Expected results:**
- DNS resolves to an IP address
- Ping responds or times out (both OK at this point)
- Connection to port 1433 succeeds

**Actual results:**
```
[Paste your output here]
```

---

### Step 3: Check MonsterASP Firewall

1. **Login to MonsterASP**
   - Go to https://www.monsterasp.net/
   - Click "My Services" or "Client Login"
   - Enter credentials

2. **Navigate to Database Management**
   - Find "My Databases" or "Database Management"
   - Select database "db67905"

3. **Find Firewall/Remote Access Settings**
   - Look for "Remote SSMS Access" tab
   - Or "Firewall" settings
   - Or "IP Whitelist"

4. **Check Current Whitelisted IPs**
   - Note which IPs are allowed
   - Is YOUR IP listed?

5. **Add Your IP if Missing**
   - Click "Add IP" or similar
   - Enter your IP address (from Step 1)
   - Click "Save" or "Enable"
   - Wait 1-2 minutes for changes to apply

**Screenshot or notes:**
```
[Describe what you see in MonsterASP control panel]
```

---

### Step 4: Test SSMS Connection

Before trying migrations again, test SSMS first:

1. **Open SQL Server Management Studio**
2. **Connection Details:**
   - Server name: `db67905.public.databaseeasp.net,1433`
   - Authentication: SQL Server Authentication
   - Login: `db67905`
   - Password: `rq+3ATs+95Lg+`
3. **Click "Connect"**

**Result:**
- ✅ Connected successfully → Proceed to migrations
- ❌ Error → Report the error below

**Error message (if any):**
```
[Paste SSMS connection error here]
```

---

### Step 5: Retry Migration

Once SSMS connects successfully, retry the migration:

```powershell
cd "C:\Users\Admin\source\repos\SNAP\SNAP.infrastructure"
dotnet ef database update --context MasterErpDbContext --verbose
```

---

## 🆘 If Still Not Working

### Check MonsterASP Service Status
- Visit https://www.monsterasp.net/
- Look for any service status/maintenance notices
- Databases might be temporarily unavailable

### Contact MonsterASP Support
- Email: Support via MonsterASP control panel
- Website: https://www.monsterasp.net/
- Provide them:
  - Your account email
  - Database names (db67905, db67904)
  - Error: "Cannot connect to database, DNS resolution failed (11001)"
  - Your IP address

### Alternative: Try from Different Network
- Try from a coffee shop or mobile hotspot
- If it works, it confirms firewall/IP issue
- If it fails, might be MonsterASP service issue

---

## 📋 Checklist

- [ ] Found my current IP address
- [ ] Tested connectivity to MonsterASP server
- [ ] Logged into MonsterASP control panel
- [ ] Found database firewall/IP whitelist settings
- [ ] Added my IP to firewall whitelist
- [ ] Waited 1-2 minutes for firewall to update
- [ ] Tested SSMS connection - SUCCESS ✅
- [ ] Retried migration command
- [ ] Migration succeeded ✅

---

## 📞 Next Steps

Once you've completed the steps above and verified SSMS connection works:

1. Reply with confirmation that SSMS connects successfully
2. I will retry the migration commands
3. Tables will be created in your MonsterASP databases

**Don't proceed to migrations until SSMS connection is verified!**
