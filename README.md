# D3Bet

Moderni sazkovy system postaveny na `C#`, `WPF`, `.NET 10`, `ASP.NET Core`, `EF Core`, `SignalR`, `ASP.NET Identity` a `OpenIddict`.

## Co projekt umi

- interni WPF klient pro provozovatele
- serverovou cast s `OAuth2/OpenID Connect`
- role `Admin` a `Operator`
- kiosk a customer display scenare
- dynamicke prepocitavani kurzu
- auditni stopu provoznich akci
- realtime synchronizaci pres `SignalR`

## Architektura

- [src/BettingApp.Wpf](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Wpf)
  WPF operator klient
- [src/BettingApp.Server](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Server)
  ASP.NET Core backend, Identity, OpenIddict, API a discovery
- [src/BettingApp.Application](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Application)
  aplikacni logika
- [src/BettingApp.Infrastructure](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Infrastructure)
  persistence, EF Core, realtime
- [src/BettingApp.Domain](C:/Users/tvanek/OneDrive%20-%20Seyfor/Dokumenty/New%20project%202/src/BettingApp.Domain)
  domenove entity

## Spusteni

### 1. Build solution

```powershell
dotnet build BettingApp.slnx
```

### 2. Spusteni serveru

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj
```

Server standardne posloucha na `http://localhost:5103`.

### 3. Spusteni WPF klienta

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

Klient podporuje automaticke discovery serveru v lokalni siti a pri startu si backend zkusi najit sam.

## Vychozi development ucty

- `admin` / `Admin1234`
- `operator` / `Operator1234`

Tyto hodnoty jsou urcene jen pro lokalni vyvoj a mely by byt pro realny provoz zmeneny.

## OAuth klienti

- operator klient: `authorization_code + PKCE`
- kiosk klient: `client_credentials`

## Stav repozitare

Repo obsahuje server i klienta v jednom solution souboru:

```powershell
BettingApp.slnx
```
