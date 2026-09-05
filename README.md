# HIRAYA Learning Center Management System

HLCMS is a .NET MAUI Blazor Hybrid app (Windows admin, Android family) plus a public website (`Hiraya.Web`), all talking to one ASP.NET Core API and MySQL.

## Architecture

```
MAUI (desktop admin + mobile family)  ─┐
Hiraya.Web (public site + web portal) ─┴→  Hiraya.Api (Kestrel)  →  MySQL (XAMPP)
```

Clients never open a MySQL connection. Database credentials stay in the API (`appsettings`, user secrets, or `HIRAYA_MYSQL_PASSWORD`).

Local JSON (`AppData/.data/hiraya-db.json`) remains as a fallback when `HirayaApi:UseRemoteStore` is `false`.

## XAMPP MySQL (development)

1. Install [XAMPP](https://www.apachefriends.org/).
2. Start **MySQL** in the XAMPP Control Panel. Start Apache only if you want phpMyAdmin.
3. Open phpMyAdmin (`http://localhost/phpmyadmin`).
4. Create a database named `hiraya_learning_center` (utf8mb4) **or** let EF create/update it on first API start.
5. Connection is configured in `Hiraya.Api/appsettings.Development.json`:

```
Server=127.0.0.1;Port=3306;Database=hiraya_learning_center;User=root;Password=
```

If your XAMPP root user has a password, do **not** commit it. Set it with:

```
setx HIRAYA_MYSQL_PASSWORD "your-password"
```

or:

```
dotnet user-secrets set ConnectionStrings:Hiraya "Server=127.0.0.1;Port=3306;Database=hiraya_learning_center;User=root;Password=your-password" --project Hiraya.Api
```

6. Run the API (Kestrel, **not** Apache):

```
dotnet run --project Hiraya.Api --launch-profile Hiraya.Api
```

First start applies migrations (or `EnsureCreated`) and seeds development users.

7. Run the MAUI Windows app. It calls `http://127.0.0.1:5188`.

Health check: `http://127.0.0.1:5188/api/health`

## Public website

With the API running:

```
dotnet run --project Hiraya.Web --launch-profile Hiraya.Web
```

Open `http://127.0.0.1:5288` for home, about, programs, enrollment, contact, and login. Guests never load the internal store; enrollment posts to `/api/public/enrollment`.

## Run on Android (emulator)

The Windows app can use `http://127.0.0.1:5188`. **The phone/emulator cannot.** `127.0.0.1` on Android is the device itself, not your PC. Debug builds automatically use `http://10.0.2.2:5188` on Android (that address is your Windows machine from the emulator).

### One-time setup on Windows

1. Install **Visual Studio 2022/2026** with workloads:
   - .NET Multi-platform App UI development (MAUI)
   - Android SDK / emulator (included with that workload)
2. Open a Developer PowerShell and confirm:

```
dotnet workload install maui
dotnet workload list
```

You should see `maui-android`.

3. In Visual Studio: **Tools → Android → Android Device Manager** → create/start a device (Pixel 5, API 34 or 35 is fine). Wait until the emulator home screen appears.

### Every time you run mobile

1. Start **XAMPP MySQL**.
2. Start the API on the PC:

```
dotnet run --project Hiraya.Api --launch-profile Hiraya.Api
```

Confirm `http://127.0.0.1:5188/api/health` opens in the PC browser.

3. In Visual Studio, open `Hiraya.slnx` (or the `Hiraya` MAUI project).
4. At the top, set the debug target to the **Android emulator** (not “Windows Machine”).
5. Press **F5**. First Android build can take several minutes.

Or from a terminal (emulator already running):

```
dotnet build Hiraya.csproj -t:Run -f net10.0-android
```

### If the app opens but login fails

- The API must stay running on the PC.
- Use the **Android emulator**, not a USB phone, until you set a LAN URL (see below).
- Do not change `BaseUrl` back to `127.0.0.1` for Android.

### Physical Android phone (USB)

`10.0.2.2` only works in the **emulator**. On a real phone:

1. Put the phone and PC on the same Wi‑Fi.
2. On the PC, run `ipconfig` and note the IPv4 (example `192.168.1.23`).
3. Start the API so it listens on all interfaces:

```
dotnet run --project Hiraya.Api --urls http://0.0.0.0:5188
```

4. Allow port **5188** in Windows Firewall for private networks.
5. Temporarily set `HirayaApi:BaseUrl` in `Resources/Raw/appsettings.json` to `http://192.168.1.23:5188` (your IP). The Android override in code only replaces localhost, so a LAN IP is kept.
6. Enable **USB debugging** on the phone, trust the PC, select the device in Visual Studio, then F5.

### iPhone

You cannot build iOS from this Windows PC. That needs a Mac with Xcode.

### EF Core migrations

```
dotnet ef migrations add InitialCreate --project Hiraya.Data --startup-project Hiraya.Api
dotnet ef database update --project Hiraya.Data --startup-project Hiraya.Api
```

Pomelo 9 + EF Core 9 run on this .NET 10 solution (official Pomelo EF 10 packages are not stable yet).

## Development logins (seed)

| Role | Email | Password |
| --- | --- | --- |
| SuperAdmin | admin@hiraya.local | Admin123 |
| Admin | ops@hiraya.local | Admin123 |
| Teacher | teacher@hiraya.local | Teacher123 |
| Staff | staff@hiraya.local | Staff123 |
| Parent | parent@hiraya.local | Parent123 |

These are development accounts only.
