# D3Bet

[![Platforma](https://img.shields.io/badge/platforma-WPF%20%2B%20ASP.NET%20Core-0f172a?style=for-the-badge)](https://github.com/AstraidLabs/D3BET)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=for-the-badge)](https://dotnet.microsoft.com/)
[![Realtime](https://img.shields.io/badge/realtime-SignalR-16a34a?style=for-the-badge)](https://learn.microsoft.com/aspnet/core/signalr/)
[![Bezpečnost](https://img.shields.io/badge/bezpečnost-Identity%20%2B%20OpenIddict-f97316?style=for-the-badge)](https://documentation.openiddict.com/)

Chytré řízení sázek, živý provozní přehled a zákaznický display v jednom propojeném řešení.

## Přehled

D3Bet je moderní platforma pro správu sázek v reálném čase. Propojuje rychlou práci operátora, živý přehled provozu, zákaznický kiosk a bezpečnou serverovou architekturu do jednoho řešení, které působí jako skutečný produkt, ne jen interní nástroj.

Projekt je postavený na `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity` a `OpenIddict`.

## ✨ Hlavní přínosy

- rychlá desktopová obsluha pro provozovatele v `WPF`
- bezpečná architektura `server <-> client` s `OAuth2` a `OpenID Connect`
- role `Admin` a `Operator` pro oddělení citlivých oprávnění
- zákaznický display režim pro kiosky a velké obrazovky
- dynamické oceňování kurzů podle aktivity trhu
- auditní stopa citlivých provozních akcí
- živá synchronizace přes `SignalR`
- automatické nalezení serveru v lokální síti

## 🧩 Architektura řešení

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

## 🚀 Rychlý start

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

Klient podporuje automatické nalezení backendu v lokální síti a při startu se pokusí server najít bez ruční konfigurace URL.

## 🔐 Výchozí vývojové účty

- `admin` / `Admin1234`
- `operator` / `Operator1234`

Tyto účty jsou určené jen pro lokální vývoj a testování. Pro reálný provoz je potřeba bootstrap hesla změnit.

## 🛡️ Autentizace a přístup

- operátorský klient: `authorization_code + PKCE`
- kiosk klient: `client_credentials`
- interní role: `Admin`, `Operator`

## 📦 Obsah repozitáře

- solution: `BettingApp.slnx`
- changelog: [CHANGELOG.md](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/CHANGELOG.md)
