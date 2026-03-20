# D3Bet

[![Platform](https://img.shields.io/badge/platform-WPF%20%2B%20ASP.NET%20Core-0f172a?style=for-the-badge)](https://github.com/AstraidLabs/D3BET)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=for-the-badge)](https://dotnet.microsoft.com/)
[![Realtime](https://img.shields.io/badge/realtime-SignalR-16a34a?style=for-the-badge)](https://learn.microsoft.com/aspnet/core/signalr/)
[![Security](https://img.shields.io/badge/security-Identity%20%2B%20OpenIddict-f97316?style=for-the-badge)](https://documentation.openiddict.com/)

Smart betting operations, live trading insight, and customer-facing display experiences in one connected platform.

---

## 🇨🇿 Česká verze

### Přehled

D3Bet je moderní platforma pro správu sázek v reálném čase. Propojuje rychlou práci operátora, živý přehled provozu, zákaznický kiosk a bezpečnou serverovou architekturu do jednoho řešení, které působí jako skutečný produkt, ne jen interní nástroj.

Projekt je postavený na `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity` a `OpenIddict`.

### ✨ Hlavní přínosy

- rychlá desktopová obsluha pro provozovatele v `WPF`
- bezpečná architektura `server <-> client` s `OAuth2` a `OpenID Connect`
- role `Admin` a `Operator` pro oddělení citlivých oprávnění
- zákaznický display režim pro kiosky a velké obrazovky
- dynamické oceňování kurzů podle aktivity trhu
- auditní stopa citlivých provozních akcí
- živá synchronizace přes `SignalR`
- automatické discovery serveru v lokální síti

### 🧩 Architektura řešení

- [src/BettingApp.Wpf](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Wpf)
  Operátorský desktop klient pro správu sázek, dashboard a administraci.
- [src/BettingApp.Server](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Server)
  Backend s API, autentizací, autorizací, discovery a auditní vrstvou.
- [src/BettingApp.Application](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Application)
  Aplikační logika, obchodní pravidla, commandy a query.
- [src/BettingApp.Infrastructure](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Infrastructure)
  Persistence, `EF Core`, databázová inicializace a realtime infrastruktura.
- [src/BettingApp.Domain](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Domain)
  Doménové entity a jádro sázkového modelu.

### 🚀 Rychlý start

#### 1. Build solution

```powershell
dotnet build BettingApp.slnx
```

#### 2. Spuštění serveru

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj
```

Výchozí adresa backendu je `http://localhost:5103`.

#### 3. Spuštění WPF klienta

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

Klient podporuje automatické nalezení backendu v lokální síti a při startu se pokusí server najít bez ruční konfigurace URL.

### 🔐 Výchozí vývojové účty

- `admin` / `Admin1234`
- `operator` / `Operator1234`

Tyto účty jsou určené jen pro lokální vývoj a testování. Pro reálný provoz je potřeba bootstrap hesla změnit.

### 🛡️ Autentizace a přístup

- operátorský klient: `authorization_code + PKCE`
- kiosk klient: `client_credentials`
- interní provoz: role `Admin`, `Operator`

### 📦 Obsah repozitáře

- solution: `BettingApp.slnx`
- changelog: [CHANGELOG.md](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/CHANGELOG.md)

---

## 🇬🇧 English Version

### Overview

D3Bet is a modern real-time betting operations platform. It brings together fast operator workflows, live operational insight, customer-facing kiosk screens, and a secure server-driven architecture in one cohesive product experience.

The platform is built with `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity`, and `OpenIddict`.

### ✨ Core Value

- fast operator workflows in a dedicated `WPF` desktop client
- secure `server <-> client` architecture with `OAuth2` and `OpenID Connect`
- `Admin` and `Operator` roles for clear permission boundaries
- customer display mode for kiosks and large public screens
- dynamic odds calculation driven by market activity
- audit trail for sensitive operational actions
- live synchronization through `SignalR`
- automatic backend discovery on the local network

### 🧩 Solution Architecture

- [src/BettingApp.Wpf](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Wpf)
  Operator desktop client for betting workflows, dashboards, and administration.
- [src/BettingApp.Server](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Server)
  Backend API with authentication, authorization, discovery, and auditing.
- [src/BettingApp.Application](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Application)
  Application layer with business rules, commands, and queries.
- [src/BettingApp.Infrastructure](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Infrastructure)
  Persistence, `EF Core`, database setup, and realtime infrastructure.
- [src/BettingApp.Domain](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Domain)
  Core domain entities and betting model.

### 🚀 Quick Start

#### 1. Build the solution

```powershell
dotnet build BettingApp.slnx
```

#### 2. Run the server

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj
```

The default backend address is `http://localhost:5103`.

#### 3. Run the WPF client

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

At startup, the client can automatically discover the backend on the local network and connect without manual endpoint setup.

### 🔐 Default Development Accounts

- `admin` / `Admin1234`
- `operator` / `Operator1234`

These credentials are intended for local development only and should be replaced before any real deployment.

### 🛡️ Authentication and Access

- operator client: `authorization_code + PKCE`
- kiosk client: `client_credentials`
- internal roles: `Admin`, `Operator`

### 📦 Repository Layout

- solution: `BettingApp.slnx`
- changelog: [CHANGELOG.md](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/CHANGELOG.md)
