# D3Bet

[![Platforma](https://img.shields.io/badge/platforma-WPF%20%2B%20ASP.NET%20Core-0f172a?style=for-the-badge)](https://github.com/AstraidLabs/D3BET)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=for-the-badge)](https://dotnet.microsoft.com/)
[![Realtime](https://img.shields.io/badge/realtime-SignalR-16a34a?style=for-the-badge)](https://learn.microsoft.com/aspnet/core/signalr/)
[![Security](https://img.shields.io/badge/security-Identity%20%2B%20OpenIddict-f97316?style=for-the-badge)](https://documentation.openiddict.com/)

Profesionální sázková platforma pro provozovatele i hráče, postavená nad `WPF`, `ASP.NET Core`, `OpenIddict`, `EF Core` a vlastním kreditním modelem `D3Kredit`.

## Přehled

D3Bet spojuje provozní správu sázek, hráčský účet, zákaznický self-service, správu financí, kreditní ekonomiku a administraci do jednoho řešení. Desktopový klient ve `WPF` slouží jako hlavní pracovní prostředí pro obsluhu i administrátory a backend v `ASP.NET Core` zajišťuje API gateway, autentizaci, audit, licenční kontrolu, obchodní logiku a realtime komunikaci.

Platforma je navržená tak, aby pokryla:

- interní provoz sázkové kanceláře
- správu hráčských účtů a profilů
- sázení nad virtuální měnou `D3Kredit`
- přepočet mezi kreditem a reálnou měnou
- správu výher, refundací, výběrů a elektronických dokladů
- bezpečný desktopový přístup bez browser-based login flow

## Hlavní funkce

### Provoz a sázky

- dashboard provozu s přehledem aktivit, událostí a sázek
- správa událostí a sázkových trhů
- přijetí, úprava a storno sázky
- živé aktualizace přes `SignalR`
- audit provozních a administrativních akcí

### Hráčský účet a zákaznický pohled

- samostatný hráčský dashboard ve `WPF`
- registrace účtu, aktivace, reaktivace a obnova přístupu
- správa osobního profilu
- přehled vlastních sázek, zůstatku a historie transakcí
- možnost dobíjet kredit a sázet na události
- přehled výher, výběrů a elektronických dokladů

### D3Kredit a finanční logika

- virtuální kredit `D3Kredit` jako hlavní herní měna
- wallet a ledger evidence pro každého hráče
- převod mezi reálnou měnou a `D3Kredit`
- vlastní kurzový model a pravidla přepočtu
- participation a market pressure logika pro úpravu hodnoty sázky
- testovací dobití kreditu přes fake payment gateway
- příprava na napojení reálné platební brány přes gateway rozhraní

### Výhry, refundace a výběry

- automatické i ruční připsání výhry
- reverze vyplacené výhry
- vrácení kreditu při refundaci sázky
- schvalování nebo zamítání výběrů kreditu zpět do měny
- evidence stavu payout a withdrawal procesů

### Elektronické doklady

- automatická evidence elektronických dokladů k dobití
- evidence dokladů k výplatě výhry
- evidence dokladů k výběru kreditu zpět do měny
- přehled dokladů pro hráče i administrátora

### Správa uživatelů

- grid seznam uživatelů s filtrováním, řazením a stránkováním
- vytvoření, editace a smazání uživatele
- správa rolí
- aktivace, deaktivace, blokace a odblokování účtu
- editace hráčského profilu, sázek a kreditních údajů
- samostatné administrační okno pro detail uživatele

### Administrace a konfigurace

- oddělená administrátorská sekce `Konfigurace`
- provozní nastavení klienta
- nastavení provizí
- nastavení kurzového modelu
- nastavení burzovního modelu
- správa banky, plateb a transakční logiky
- správa kreditní ekonomiky a finančního chování systému
- licenční přehled a správa klientských licencí

### E-mail a account flow

- self-service account flow bez webového prohlížeče
- preview režim e-mailů pro vývoj
- MailKit sender
- příprava pro vlastní gateway s OAuth2
- aktivace účtu a reset hesla přes řízený account proces

### Bezpečnost a přístup

- `ASP.NET Identity` pro správu účtů
- `OpenIddict` pro tokeny, role a přístupové toky
- oddělení rolí `Admin`, `Operator`, `Customer`
- desktop login bez nutnosti externího browseru
- licenční kontrola klienta před přihlášením
- auditní stopa citlivých zásahů

## Licencování a aktivace klienta

D3Bet klient při prvním spuštění vyžaduje platnou licenci přiřazenou k dané instalaci. Aktivace je navržená tak, aby byla jednoduchá pro oprávněného uživatele a současně bezpečná pro provozní nasazení.

Prakticky to znamená:

- při prvním spuštění klient otevře aktivační obrazovku
- uživatel zadá licenční e-mail a aktivační klíč od správce systému
- klient ověří oprávnění vůči serveru
- po úspěšném ověření se klient bezpečně připraví na načtení přihlašovací konfigurace
- teprve potom se otevře standardní přihlášení do aplikace

Z bezpečnostních důvodů README záměrně nepopisuje interní validační a ochranné mechanismy licence do technického detailu. Pro běžný provoz stačí vědět, že:

- jedna licence je určená pro autorizovanou instalaci klienta
- při změně zařízení nebo reinstalaci může být potřeba licenci znovu uvolnit správcem
- při problému s aktivací klient zobrazí srozumitelnou chybovou hlášku a neumožní pokračovat do loginu bez platné licence

## Role v systému

- `Admin`
  Nejvyšší oprávnění. Správa uživatelů, konfigurace, licencí, financí, kreditního modelu, výplat, refundací a provozních nastavení.
- `Operator`
  Provozní obsluha systému, práce s událostmi, sázkami a každodenním provozem.
- `Customer`
  Hráčský účet s přístupem do vlastního dashboardu, profilu, kreditu, sázek a self-service funkcí.

## Architektura řešení

- [src/BettingApp.Wpf](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Wpf)
  Desktopový klient ve `WPF` pro administrátory, obsluhu a hráčský pohled.
- [src/BettingApp.Server](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Server)
  API gateway, autentizace, autorizace, account flow, licenční vrstva, e-mail, realtime a obchodní orchestrace.
- [src/BettingApp.Application](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Application)
  Aplikační use-cases, commandy, query a obchodní pravidla.
- [src/BettingApp.Infrastructure](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Infrastructure)
  Persistence, `EF Core`, migrace, databázová inicializace a technická infrastruktura.
- [src/BettingApp.Domain](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Domain)
  Doménové entity a základní model systému.

## Technologie

- `C#`
- `.NET 10`
- `WPF`
- `ASP.NET Core`
- `EF Core`
- `SQLite`
- `SignalR`
- `ASP.NET Identity`
- `OpenIddict`
- `MailKit`

## Rychlý start pro vývoj

### 1. Build řešení

```powershell
dotnet build BettingApp.slnx
```

### 2. Spuštění serveru

```powershell
dotnet run --project src/BettingApp.Server/BettingApp.Server.csproj --launch-profile http
```

Výchozí lokální adresa serveru je `http://localhost:5103`.

### 3. Spuštění WPF klienta

```powershell
dotnet run --project src/BettingApp.Wpf/BettingApp.Wpf.csproj
```

Klient je navržený jako desktopová aplikace. Přihlášení i aktivace probíhají přímo uvnitř `WPF` rozhraní.

## Lokální vývojové účty

Tyto účty jsou určené pouze pro lokální vývoj a testování:

- `admin` / `Admin1234`
- `operator` / `Operator1234`
- `player` / `Player1234`

V produkčním nebo sdíleném prostředí je potřeba výchozí přístupy změnit.

## Lokální vývojová licence

Pro vývojovou aktivaci klienta je v lokální konfiguraci připravená testovací licence:

- `client@d3bet.local`
- `RDNCRVQtU0lOR0xFLUxJQ0VOU0UtMjAyNg==`

Tento údaj je určený jen pro lokální development prostředí.

## Konfigurace prostředí

Hlavní provozní nastavení je v:

- [src/BettingApp.Server/appsettings.json](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Server/appsettings.json)
- [src/BettingApp.Server/appsettings.Development.json](C:/Users/tvanek/source/repos/D3BET/src/BettingApp.Server/appsettings.Development.json)

Najdete zde zejména:

- OAuth klienty
- bootstrap identity účty
- discovery nastavení
- account e-mail sender
- licenční bootstrap nastavení
- `D3Kredit` pravidla

## Provozní poznámky

- server podporuje lokální discovery v síti
- e-mailová vrstva může běžet v preview režimu, přes `smtp4dev` nebo přes vlastní gateway
- licenční a bootstrap tok je řízen serverem a předchází samotnému přihlášení
- administrace obsahuje audit a přehled důležitých zásahů

## Obsah repozitáře

- solution: `BettingApp.slnx`
- změny a vývojové poznámky: [CHANGELOG.md](C:/Users/tvanek/source/repos/D3BET/CHANGELOG.md)

## Stav projektu

D3Bet je rozsáhlá desktopově-serverová platforma s provozní, hráčskou, finanční i administrativní vrstvou. README shrnuje funkční možnosti a vývojový start. Podrobnější provozní pravidla, bezpečnostní politiky a interní ochranné mechanismy jsou záměrně vedené mimo veřejný přehled tohoto souboru.
