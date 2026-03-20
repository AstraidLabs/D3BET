# D3Bet

**CZ**

D3Bet je moderní platforma pro správu sázek v reálném čase. Spojuje rychlou obsluhu přepážky, živý přehled provozu, zákaznický kiosk a bezpečnou serverovou architekturu do jednoho řešení, které je připravené pro další růst.

Projekt je postavený na `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity` a `OpenIddict`.

## Proč D3Bet

- rychlá interní práce provozovatele v desktopovém `WPF` klientu
- serverová architektura `server <-> client` s `OAuth2/OpenID Connect`
- role `Admin` a `Operator` pro bezpečné oddělení oprávnění
- zákaznický display pro kiosk a velké obrazovky
- dynamické oceňování kurzů podle aktivity trhu
- auditní stopa citlivých provozních akcí
- živá synchronizace přes `SignalR`
- automatické nalezení serveru v lokální síti

## Architektura

- [src/BettingApp.Wpf](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Wpf)
  Operátorský desktop klient pro správu sázek, dashboard a administraci.
- [src/BettingApp.Server](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Server)
  Backend s API, autentizací, autorizací, discovery a auditní vrstvou.
- [src/BettingApp.Application](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Application)
  Aplikační logika, commandy, query a obchodní pravidla.
- [src/BettingApp.Infrastructure](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Infrastructure)
  Persistence, `EF Core`, databázová inicializace a realtime infrastruktura.
- [src/BettingApp.Domain](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Domain)
  Doménové entity a jádro sázkového modelu.

## Rychlý start

### 1. Build solution

```powershell
dotnet build BettingApp.slnx
```

### 2. Spuštění serveru

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj
```

Výchozí adresa backendu je `http://localhost:5103`.

### 3. Spuštění WPF klienta

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

Klient při startu podporuje automatické discovery serveru v lokální síti a pokusí se backend najít bez ruční konfigurace.

## Výchozí vývojové účty

- `admin` / `Admin1234`
- `operator` / `Operator1234`

Tyto účty jsou určené jen pro lokální vývoj a testování. Pro reálný provoz je potřeba hesla i bootstrap konfiguraci změnit.

## OAuth klienti

- operátorský klient: `authorization_code + PKCE`
- kiosk klient: `client_credentials`

## Stav repozitáře

Repozitář obsahuje server i klienta v jednom solution souboru:

```powershell
BettingApp.slnx
```

---

**EN**

D3Bet is a modern betting operations platform built for real-time workflows. It combines a fast operator desktop experience, live business insight, customer-facing kiosk displays, and a secure server-driven architecture in one product-ready solution.

The stack is powered by `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity`, and `OpenIddict`.

## Why D3Bet

- fast internal operator workflows in a dedicated `WPF` desktop client
- `server <-> client` architecture with `OAuth2/OpenID Connect`
- `Admin` and `Operator` roles for clear security boundaries
- customer display mode for kiosks and large public screens
- dynamic odds calculation driven by market activity
- audit trail for sensitive operational actions
- live synchronization through `SignalR`
- automatic local-network server discovery

## Architecture

- [src/BettingApp.Wpf](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Wpf)
  Operator desktop client for betting operations, dashboards, and administration.
- [src/BettingApp.Server](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Server)
  Backend API with authentication, authorization, discovery, and audit services.
- [src/BettingApp.Application](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Application)
  Application logic, commands, queries, and business rules.
- [src/BettingApp.Infrastructure](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Infrastructure)
  Persistence, `EF Core`, database initialization, and realtime infrastructure.
- [src/BettingApp.Domain](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Domain)
  Core domain entities and betting model.

## Quick Start

### 1. Build the solution

```powershell
dotnet build BettingApp.slnx
```

### 2. Run the server

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj
```

The default backend address is `http://localhost:5103`.

### 3. Run the WPF client

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

At startup, the client can automatically discover the server on the local network and connect without manual endpoint setup.

## Default development accounts

- `admin` / `Admin1234`
- `operator` / `Operator1234`

These credentials are intended for local development only. Update passwords and bootstrap settings before any real deployment.

## OAuth clients

- operator client: `authorization_code + PKCE`
- kiosk client: `client_credentials`

## Repository layout

The repository contains both server and client in a single solution:

```powershell
BettingApp.slnx
```
