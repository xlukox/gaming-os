# Gaming OS – architektúra a plán vývoja

Stav: **plán schválený 24. 11. 2026** (rozhodnutia v kapitole 11) · Repozitár založený, kód sa ešte nezačal.

---

## 0. Zhrnutie a kľúčové rozhodnutia

| # | Rozhodnutie | Odporúčanie |
|---|---|---|
| 1 | Čo je produkt | Launcher sám o sebe nestačí. Od 30. 4. 2026 má Windows 11 vlastný **Xbox mode** (konzolové prostredie na celú obrazovku), zadarmo existujú Playnite, Steam Big Picture a GOG Galaxy. Odlíšiť sa dá len tým, čo nemajú: **AI Game Doctor, merané výkonnostné relácie a herný život (štatistiky, Wrapped)**. Knižnica je vstupenka, nie produkt. |
| 2 | Desktop stack | **.NET 10 (WPF okno + WebView2) + React/TypeScript.** Tauri vypustiť – vynútil by tretí jazyk (Rust) popri C#, ktoré aj tak potrebujeme. Detaily v 1.2. |
| 3 | Dáta | **Local-first.** SQLite na disku používateľa, žiadny vlastný backend vo verzii 1. Nulové prevádzkové náklady. |
| 4 | AI | Tri vrstvy: **pravidlá (zadarmo) → lokálny model cez Ollama → BYOK**. Jedno rozhranie, žiadne naše API náklady. |
| 5 | Integrácie | V1 iba **lokálna detekcia + oficiálne API**. Epic a GOG nemajú oficiálne API pre tretie strany → vlastné sledovanie aktivity. Neoficiálne prihlásenia nepoužívame. |
| 6 | Bezpečnosť | UI beží bez admin práv. Všetko privilegované robí samostatný **Helper** s úzkym zoznamom operácií; každá zmena má `Apply` + `Revert` a zápis v žurnáli. Defender, Windows Update a anti-cheat sú mimo dosahu kódu. |
| 7 | MVP | Shell s gamepadom + knižnica (Steam/Epic/GOG) + sledovanie aktivity + prehľad hardvéru + Doctor v0 (pravidlá). |
| 8 | Náklady | 0 € do verejnej bety (okrem domény ~10–15 €/rok). |

---

## 1. Technická architektúra

### 1.1 Prehľad

```
┌──────────────────────────── GamingOS.exe (bez admin práv) ───────────────────────────┐
│                                                                                       │
│  UI: React + TypeScript (WebView2)                                                    │
│  ┌─────────────┬──────────────┬──────────────┬─────────────┬──────────────┐          │
│  │ shell       │ features/    │ features/    │ features/   │ kit          │          │
│  │ (navigácia, │ library      │ activity     │ doctor …    │ (design      │          │
│  │ vstup, téma)│              │              │             │ systém)      │          │
│  └─────────────┴──────────────┴──────────────┴─────────────┴──────────────┘          │
│                     ▲  JSON-RPC cez WebView2 postMessage  ▼                           │
│  Host (.NET 10): okno, IPC router, autostart, aktualizácie, tray                      │
│  ┌──────────┬──────────┬──────────┬──────────┬──────────┬──────────┬──────────┐      │
│  │ Library  │ Activity │ System   │ Doctor   │ AI       │ Intel    │ …        │      │
│  └────┬─────┴────┬─────┴────┬─────┴────┬─────┴────┬─────┴────┬─────┴──────────┘      │
│       └──────────┴──────────┴─── Core ─┴──────────┴──────────┘                       │
│            (kontrakty, event bus, SQLite, nastavenia, plánovač úloh)                  │
└───────────────┬──────────────────────────────────────────────┬───────────────────────┘
                │ named pipe (ACL, whitelist príkazov)          │ HTTPS (len na vyžiadanie)
   ┌────────────▼────────────┐                     ┌───────────▼────────────────────┐
   │ GamingOS.Helper.exe     │                     │ Steam Web API · news           │
   │ (admin, na požiadanie,  │                     │ AI poskytovateľ (BYOK)         │
   │ od fázy 3): senzory,    │                     │ Ollama na localhost            │
   │ PresentMon, tweaky      │                     │ metadáta/obaly                 │
   └─────────────────────────┘                     └────────────────────────────────┘
```

### 1.2 Vyhodnotenie stacku

| Variant | Jazyky | Plusy | Mínusy |
|---|---|---|---|
| A. Tauri 2 + Rust (bez C#) | TS, Rust | malý inštalátor (~10 MB), hotový updater a pluginy | senzory (LibreHardwareMonitor), Event Log, WMI, ETW sú v Ruste prácne alebo neexistujú; pomalšie buildy; agenti míňajú viac limitu na boj s kompilátorom |
| B. Tauri 2 + .NET sidecar (pôvodný zámer) | TS, Rust, C# | to isté + .NET knižnice | **tri jazyky**, dva procesy od prvého dňa, IPC cez dve vrstvy |
| **C. .NET host + WebView2 + React** | **TS, C#** | jeden proces, jeden backend jazyk; všetky Windows API a knižnice priamo; rýchle buildy | inštalátor ~70 MB; updater a tray treba poskladať (Velopack, pár riadkov) |
| D. Electron | TS | jeden jazyk | 300+ MB RAM popri hre, Windows integrácia cez natívne addony |

**Schválený variant C (24. 11. 2026).** Produkt je výhradne pre Windows a väčšina hodnoty (Doctor, senzory, Boost, System Control) je systémová integrácia, kde je .NET najkratšia cesta. Rust by bol len lepidlo, ktoré musí niekto udržiavať. Veľkosť inštalátora je pri hráčoch so 100 GB hrami nepodstatná. WebView2 je vo Windows 11 predinštalovaný.

Variant B zostáva funkčná alternatíva, ak má tím silný dôvod pre Tauri (napr. existujúce skúsenosti s Rustom). Kontrakt UI ↔ backend (1.4) je navrhnutý tak, že UI o hostiteľovi nevie – výmena hostiteľa neznamená prepis UI.

### 1.3 Shell a „konzolový“ zážitok

- **Okno:** WPF, bez rámu, celá obrazovka; prepínateľné do okna (F11).
- **Štart po prihlásení:** záznam v `HKCU\...\Run` (vypínateľné v nastaveniach). **Nenahrádzame shell Windows** (`explorer.exe`) – je to krehké, na Windows Home nepodporované a pri chybe zablokuje prihlásenie. Prehodnotiť najskôr po verejnej bete.
- **Vstup:** jedna abstrakcia akcií (`up/down/left/right/confirm/back/menu`) napájaná z klávesnice, myši a Gamepad API. Fokus rieši `@noriginmedia/norigin-spatial-navigation` (MIT) – nepíšeme vlastný.
- **Privolanie z hry:** globálna klávesová skratka (`RegisterHotKey`) + voliteľná kombinácia na gamepade cez XInput v hoste. Bez overlay vo vnútri hry (overlay = injekcia = konflikt s anti-cheatom).
- **Počas hry:** okno sa skryje a WebView2 sa uspí (`TrySuspendAsync`). Beží len sledovanie v hoste (cieľ < 1 % CPU, < 100 MB RAM). Po skončení hry sa UI vráti do popredia.
- **TV režim:** UI v `rem`, prepínač mierky 100/125/150 %; minimálna veľkosť písma a fokus viditeľný z 3 metrov.
- **Living Interface (lite):** akcent a pozadie odvodené z obalu vybranej hry (dominantná farba → CSS premenné). Lacný efekt s veľkým dojmom, patrí už do MVP.

### 1.4 Kontrakt UI ↔ host (najdôležitejšie rozhranie)

JSON-RPC cez `window.chrome.webview.postMessage`:

```jsonc
{ "id": 42, "method": "library.list", "params": { "filter": "installed" } }   // UI → host
{ "id": 42, "result": [ /* GameDto[] */ ] }                                      // odpoveď
{ "id": 42, "error": { "code": "platform_unavailable", "message": "…" } }        // chyba
{ "event": "activity.sessionStarted", "data": { "gameId": 17 } }                 // host → UI
```

Zdroj pravdy je C#:

```csharp
[IpcModule("library")]
public interface ILibraryApi
{
    Task<GameDto[]> List(LibraryFilter filter);
    Task Launch(long gameId);
    Task Rescan();
}
```

- Z C# DTO a rozhraní sa pri builde generuje `src/ui/src/contracts/generated.ts` (typy + typovaný klient `api.library.list(...)`). Nástroj vyberieme spikom vo fáze 0 (kandidáti: TypeGen, Reinforced.Typings). CI zlyhá, ak vygenerovaný súbor nesedí.
- UI má `MockBridge` s fixtúrami implementujúci rovnaký kontrakt → **UI sa vyvíja v bežnom prehliadači cez `npm run dev` bez spusteného hostu.** Toto je hlavný nástroj paralelnej práce.
- Zmena kontraktu ide vždy samostatným malým PR (pozri 6.3).

### 1.5 AI architektúra

```
Dôkazy (logy, udalosti, hardvér)
   │
   ▼
[1] Pravidlový engine ── YAML pravidlá v repozitári, 0 €, funguje offline
   │  nálezy + chýbajúce vysvetlenie
   ▼
[2] IAiProvider (voliteľné)
   ├─ Ollama / LM Studio na localhost  ── 0 €, súkromné
   └─ BYOK: vlastný kľúč používateľa   ── platí používateľ, centy
```

- **Jedno rozhranie, dve implementácie:** `OpenAiCompatibleProvider(baseUrl, key, model)` pokrýva Ollama, LM Studio, OpenAI, OpenRouter, Groq aj Gemini (všetky majú OpenAI-kompatibilný endpoint); `AnthropicProvider` používa natívne Messages API. V UI sú to predvoľby, nie samostatný kód.
- **Lokálny model nebalíme.** Detegujeme bežiacu Ollamu; ak chýba, ukážeme návod. AI úlohy sa nespúšťajú počas hry (bojovali by o VRAM) – idú do fronty po relácii.
- **AI nikdy nič nevykonáva.** Model iba vysvetľuje a zoraďuje opravy z nášho katalógu (`tweaks`). Voľný text od modelu sa nikdy nespúšťa ako príkaz.
- **Súkromie:** pred odoslaním do cloudu sa z logov odstráni meno používateľa, cesty k profilu a názov PC; pri prvom použití používateľ vidí presný obsah, ktorý odchádza.
- **Náklady používateľa:** cache odpovedí podľa hashu (`ai_cache`), denný limit tokenov v nastaveniach. Orientačne podľa cenníka Anthropic (11/2026): súhrn patch notes na Claude Haiku 5.5 (~3 000 tokenov vstup, 300 výstup) ≈ 0,0005 USD, t. j. 1 000 súhrnov ≈ 0,45 USD; diagnostika s 20 000 tokenmi logov na Claude Sonnet 5.5 ≈ 0,05 USD.
- **Kľúče** sa ukladajú do Windows Credential Manager, nikdy do SQLite ani do súborov.
- **Predplatné Claude Pro a ChatGPT Go nie sú API.** Slúžia tímu na vývoj; aplikácia ich použiť nemôže ani nesmie.

### 1.6 Bezpečnostné hranice (nemenné pravidlá)

1. Kód neobsahuje žiadnu cestu, ktorá vypína alebo oslabuje Defender, Windows Update, firewall, UAC alebo anti-cheat.
2. Žiadna injekcia do procesov hier, žiadne čítanie ich pamäte, žiadny overlay vo vnútri hry.
3. Každá systémová zmena = položka z katalógu tweakov s `Detect / Apply / Revert`, zobrazená používateľovi pred vykonaním, zapísaná do `tweaks_journal`.
4. Relačné tweaky sa vracajú pri skončení hry; ak aplikácia spadne, vrátia sa pri ďalšom štarte (žurnál obsahuje nevrátené záznamy).
5. Helper prijíma len ID tweaku z katalógu, nikdy ľubovoľný príkaz.

---

## 2. Štruktúra repozitára

Monorepo, jeden GitHub repozitár.

```
gaming-os/
├─ CLAUDE.md                     # spoločné pravidlá (krátke, ~50 riadkov)
├─ README.md                     # ako spustiť, ako prispieť
├─ GamingOS.sln
├─ .claude/
│  ├─ settings.json              # zdieľané povolenia/zákazy pre agentov (napr. zákaz push na main)
│  └─ commands/                  # zdieľané príkazy: /new-feature, /new-connector, /pr
├─ .github/
│  ├─ workflows/ci.yml           # build + testy na PR
│  ├─ workflows/nightly.yml      # testovacia verzia po merge do main
│  ├─ pull_request_template.md
│  └─ CODEOWNERS
├─ docs/
│  ├─ PLAN.md                    # tento dokument
│  └─ adr/                       # krátke záznamy rozhodnutí (0001-stack.md …)
├─ src/
│  ├─ host/GamingOS.Host/        # WPF okno, WebView2, IPC router, kompozícia modulov
│  ├─ core/GamingOS.Core/        # kontrakty (DTO, I*Api), event bus, DB, nastavenia
│  ├─ modules/
│  │  ├─ GamingOS.Library/       # + CLAUDE.md modulu, Migrations/, Connectors/
│  │  ├─ GamingOS.Activity/
│  │  ├─ GamingOS.System/
│  │  ├─ GamingOS.Doctor/
│  │  ├─ GamingOS.AI/
│  │  └─ …                       # ďalšie pribúdajú podľa etáp
│  ├─ helper/GamingOS.Helper/    # od fázy 3
│  └─ ui/                        # React + Vite
│     └─ src/
│        ├─ shell/               # navigácia, vstup, téma, registry feature modulov
│        ├─ kit/                 # design systém (tokeny, komponenty)
│        ├─ contracts/           # generated.ts + bridge (real | mock)
│        └─ features/
│           ├─ library/          # feature.ts, komponenty, locales/, mocks/
│           ├─ activity/
│           └─ …
├─ rules/
│  ├─ doctor/*.yaml              # diagnostické pravidlá
│  └─ tweaks/*.yaml              # katalóg vratných optimalizácií
└─ tests/
   ├─ GamingOS.Library.Tests/    # + fixtures/ (reálne vzorky VDF, .item, registry exporty)
   └─ …
```

Zásady štruktúry:

- **Modul = jeden C# projekt + jeden priečinok `features/<názov>` v UI.** Modul referencuje iba `Core`, nikdy iný modul – vynúti to kompilátor.
- **Žiadne zdieľané „registračné“ súbory.** Backend moduly sa načítavajú reflexiou cez `IModule`; UI funkcie cez `import.meta.glob('./features/*/feature.ts')`. Pridanie modulu nemení žiadny spoločný súbor.
- **Každý modul má vlastné migrácie, preklady a fixtúry** vo svojom priečinku.

---

## 3. Moduly

| Modul | Zodpovednosť | Pokrýva funkcie zo zadania | Etapa |
|---|---|---|---|
| **Host** | okno, IPC, autostart, aktualizácie, skratky, uspávanie UI | – | 0 |
| **Core** | kontrakty, event bus, SQLite + migrácie, nastavenia, plánovač úloh na pozadí | – | 0 |
| **UI Shell + Kit** | navigácia, vstup (gamepad), téma, design systém, widgety | Living Interface | 0–1 |
| **Library** | konektory platforiem, zlučovanie duplicít, spúšťanie, metadáta a obaly | Game Center | 1 |
| **Activity** | sledovanie procesov, relácie, agregácie | My Gaming Life, Gaming Wrapped | 1 (zber), 2 a 4 (UI) |
| **System** | inventár hardvéru, ovládače, úložisko, sieť, aplikácie po štarte | System Control | 1 (čítanie), 3 (zmeny) |
| **Doctor** | zber dôkazov, pravidlový engine, diagnózy | AI Game Doctor | 2 |
| **AI** | poskytovatelia, cache, anonymizácia, limity | spoločné pre všetky AI funkcie | 2 |
| **Intel** | novinky a patch notes, AI súhrny | Game Intelligence | 2 |
| **Performance** | PresentMon, senzory, vzorky relácií, odporúčania | Session Replay, AI Performance Coach | 3 |
| **Boost** | katalóg tweakov, žurnál, meranie pred/po | Game Boost | 3 |
| **Helper** | privilegované operácie | (infra pre Performance, Boost, System) | 3 |
| **Backlog** | odporúčania z vlastnej knižnice | Backlog Manager | 4 |
| **Achievements** | achievementy (Steam), odporúčania | Achievement Hunter | 4 |
| **Memory** | pripomenutie príbehu a postupu | Game Memory | 4 |
| **Social** | priatelia a aktivita | Gaming Social Hub | 5 |
| **Compat** | známe problémy hra × hardvér × ovládač | Compatibility Radar | 5 |
| **Setup** | profily nastavení hier | One-Click Game Setup | 5 |
| **Voice** | hlasové ovládanie | Voice Gaming Assistant | 5 |
| **Marketplace** | témy, widgety, rozšírenia | Gaming Marketplace | 5+ |

Komunikácia medzi modulmi ide výhradne cez udalosti v `Core` (`GameLaunched`, `SessionEnded`, `LibraryChanged`, `CrashDetected`). Príklad: `Activity` vyšle `SessionEnded(crashed: true)` → `Doctor` spustí zber dôkazov. Moduly o sebe nevedia.

---

## 4. Databáza a dátové modely

- **SQLite**, režim WAL, `Microsoft.Data.Sqlite` + Dapper (bez ORM).
- Dva súbory v `%LOCALAPPDATA%\GamingOS\`: `gamingos.db` (všetko podstatné, malé, zálohovateľné) a `telemetry.db` (vzorky výkonu; dá sa zmazať bez straty knižnice).
- **Migrácie:** číslované SQL súbory v priečinku modulu, verzia sa sleduje za každý modul zvlášť v `schema_versions(module, version)` → dvaja ľudia si nikdy nevezmú rovnaké číslo.
- Tabuľky nesú prefix modulu (`lib_`, `act_`, `sys_`, `doc_`, `perf_` …).

### 4.1 Jadro schémy (`gamingos.db`)

```sql
-- LIBRARY
CREATE TABLE lib_games (
  id            INTEGER PRIMARY KEY,
  title         TEXT NOT NULL,
  sort_title    TEXT NOT NULL,
  igdb_id       INTEGER,                 -- kľúč pre zlučovanie naprieč obchodmi
  cover_path    TEXT, hero_path TEXT,    -- lokálna cache obrázkov
  accent_color  TEXT,                    -- pre Living Interface
  release_date  TEXT, genres TEXT,       -- genres = JSON pole
  est_hours     REAL,                    -- odhad dĺžky (Backlog)
  favorite      INTEGER NOT NULL DEFAULT 0,
  hidden        INTEGER NOT NULL DEFAULT 0,
  backlog_state TEXT,                    -- 'backlog' | 'playing' | 'done' | 'dropped'
  added_at      TEXT NOT NULL
);

CREATE TABLE lib_sources (                -- jedna hra môže byť vo viacerých obchodoch
  id               INTEGER PRIMARY KEY,
  game_id          INTEGER NOT NULL REFERENCES lib_games(id),
  platform         TEXT NOT NULL,        -- 'steam' | 'epic' | 'gog' | 'xbox' | 'manual'
  platform_game_id TEXT NOT NULL,
  owned            INTEGER NOT NULL,
  installed        INTEGER NOT NULL,
  install_path     TEXT, exe_path TEXT,
  launch_uri       TEXT,
  size_bytes       INTEGER,
  platform_playtime_min INTEGER,         -- NULL, ak platforma údaj neposkytuje
  platform_last_played  TEXT,
  last_seen_at     TEXT NOT NULL,
  UNIQUE (platform, platform_game_id)
);

CREATE TABLE lib_accounts (
  id INTEGER PRIMARY KEY, platform TEXT NOT NULL, account_id TEXT NOT NULL,
  display_name TEXT, auth_kind TEXT NOT NULL,      -- 'local' | 'api_key' | 'oauth'
  last_sync_at TEXT, sync_status TEXT
);                                                  -- tajomstvá sú v Credential Manageri

-- ACTIVITY
CREATE TABLE act_sessions (
  id INTEGER PRIMARY KEY,
  game_id INTEGER NOT NULL REFERENCES lib_games(id),
  source_id INTEGER REFERENCES lib_sources(id),
  started_at TEXT NOT NULL, ended_at TEXT, duration_sec INTEGER,
  origin TEXT NOT NULL,                  -- 'tracked' (vlastné meranie) | 'imported'
  crashed INTEGER NOT NULL DEFAULT 0, exit_code INTEGER
);
CREATE INDEX act_sessions_game ON act_sessions(game_id, started_at);

-- SYSTEM
CREATE TABLE sys_hardware_snapshots (     -- nový riadok iba pri zmene (hash)
  id INTEGER PRIMARY KEY, taken_at TEXT NOT NULL, hash TEXT NOT NULL,
  cpu TEXT, gpu TEXT, gpu_driver TEXT, ram_gb INTEGER, os_build TEXT,
  storage TEXT, displays TEXT             -- JSON
);

-- DOCTOR
CREATE TABLE doc_diagnoses (
  id INTEGER PRIMARY KEY, game_id INTEGER, session_id INTEGER,
  created_at TEXT NOT NULL, trigger TEXT NOT NULL,   -- 'crash' | 'manual'
  evidence TEXT NOT NULL,                 -- JSON balík dôkazov (anonymizovaný)
  rule_hits TEXT NOT NULL,                -- JSON: id pravidla, závažnosť
  ai_summary TEXT, status TEXT NOT NULL   -- 'open' | 'fixed' | 'dismissed'
);

-- BOOST / SYSTEM zmeny
CREATE TABLE sys_tweaks_journal (
  id INTEGER PRIMARY KEY, tweak_id TEXT NOT NULL,
  scope TEXT NOT NULL,                    -- 'session' | 'persistent'
  session_id INTEGER,
  applied_at TEXT NOT NULL, reverted_at TEXT,
  before_state TEXT NOT NULL, after_state TEXT NOT NULL   -- JSON; before_state stačí na návrat
);

-- INTEL
CREATE TABLE intel_news (
  id INTEGER PRIMARY KEY, game_id INTEGER NOT NULL, source TEXT NOT NULL,
  external_id TEXT NOT NULL, kind TEXT,   -- 'patch' | 'news' | 'dlc'
  title TEXT NOT NULL, url TEXT, published_at TEXT, body TEXT,
  ai_summary TEXT, read INTEGER NOT NULL DEFAULT 0,
  UNIQUE (source, external_id)
);

-- ACHIEVEMENTS
CREATE TABLE ach_achievements (
  id INTEGER PRIMARY KEY, game_id INTEGER NOT NULL, platform TEXT NOT NULL,
  api_name TEXT NOT NULL, title TEXT, description TEXT, icon_path TEXT,
  hidden INTEGER, global_pct REAL, unlocked_at TEXT,
  UNIQUE (platform, game_id, api_name)
);

-- SPOLOČNÉ
CREATE TABLE core_settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);   -- JSON
CREATE TABLE ai_cache (
  hash TEXT PRIMARY KEY, provider TEXT, model TEXT, kind TEXT,
  response TEXT NOT NULL, tokens_in INTEGER, tokens_out INTEGER, created_at TEXT NOT NULL
);
```

### 4.2 Telemetria (`telemetry.db`, od fázy 3)

```sql
CREATE TABLE perf_samples (               -- 1 riadok za sekundu hrania
  session_id INTEGER NOT NULL, t INTEGER NOT NULL,   -- sekundy od začiatku relácie
  fps_avg REAL, fps_p1 REAL, frametime_p99_ms REAL,
  cpu_load REAL, gpu_load REAL, cpu_temp REAL, gpu_temp REAL,
  ram_mb INTEGER, vram_mb INTEGER,
  PRIMARY KEY (session_id, t)
) WITHOUT ROWID;

CREATE TABLE perf_events (
  session_id INTEGER NOT NULL, t INTEGER NOT NULL,
  kind TEXT NOT NULL,                     -- 'stutter' | 'thermal_throttle' | 'crash'
  detail TEXT
);
```

Objem: ~3 600 riadkov na hodinu hrania (rádovo stovky kB). Po 30 dňoch zriedenie na 10-sekundové priemery; lehota je v nastaveniach.

### 4.3 Hrací čas – ako zlúčiť dva zdroje

`platform_playtime_min` (ak ho platforma dáva) je celoživotný súčet; `act_sessions` sú naše presné relácie od inštalácie Gaming OS. Celkový čas = hodnota z platformy, ak existuje, inak súčet vlastných relácií. Grafy v čase a Wrapped používajú iba vlastné relácie. Preto musí zber aktivity bežať **od prvej verzie**, aj keď UI štatistík príde neskôr.

---

## 5. Integrácia herných platforiem

### 5.1 Čo ktorá platforma naozaj poskytuje

Legenda: **API** = oficiálne API · **LOK** = lokálne súbory/registry (stabilné, nedokumentované) · **✗** = nedostupné bez neoficiálnych metód → riešime vlastným sledovaním alebo vôbec.

| Údaj | Steam | Epic Games | GOG |
|---|---|---|---|
| Nainštalované hry | LOK: `libraryfolders.vdf` + `appmanifest_*.acf` | LOK: `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` | LOK: registry `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games` |
| Vlastnené, nenainštalované | API: `IPlayerService/GetOwnedGames` | ✗ | ✗ (voliteľne LOK: databáza GOG Galaxy, iba na čítanie) |
| Hrací čas | API alebo LOK (`userdata/…/localconfig.vdf`) | ✗ → vlastné sledovanie | LOK z Galaxy DB, inak vlastné sledovanie |
| Achievementy | API: `ISteamUserStats` | ✗ | ✗ |
| Novinky / patch notes | API: `ISteamNews/GetNewsForApp` (bez kľúča) | ✗ | ✗ |
| Priatelia | API (iba čítanie, verejné profily) | ✗ | ✗ |
| Spustenie | `steam://rungameid/<appid>` | `com.epicgames.launcher://apps/<id>?action=launch&silent=true` | `GalaxyClient.exe /command=runGame /gameId=<id>` alebo priamo `.exe` |
| Obaly | LOK cache Steamu + Steam CDN | cez službu metadát | cez službu metadát |

Dôsledky pre produkt:

- **Achievement Hunter, Game Intelligence a Social Hub sú v praxi „Steam-only“.** UI to musí povedať otvorene (štítok „túto funkciu platforma neposkytuje“), nie predstierať prázdne dáta.
- **Epic a GOG = nainštalované hry + naše vlastné štatistiky.**
- Neoficiálne prihlásenie do Epic/GOG (ako robia Legendary/Heroic) do V1 nejde: porušuje podmienky služieb a môže sa kedykoľvek rozbiť. Prehodnotiť až na základe dopytu používateľov.

### 5.2 Steam Web API kľúč

Podmienky Valve vyžadujú kľúč držať v tajnosti a nedistribuovať ho s klientom. Náš kľúč teda do aplikácie nepatrí. Postup:

1. **V1:** všetko, čo ide, lokálne (inštalované hry, hrací čas, obaly). Vlastnené hry a achievementy po zadaní **vlastného kľúča používateľa** (zadarmo na steamcommunity.com/dev/apikey) – rovnaký vzor ako BYOK pri AI.
2. **Neskôr (ak to bude brzdiť adopciu):** tenká proxy na Cloudflare Workers s naším kľúčom a prihlásením cez Steam OpenID. Až vtedy vzniká prvý serverový komponent.

### 5.3 Rozhranie konektora

```csharp
[Flags] public enum Capabilities { Installed = 1, Owned = 2, Playtime = 4, Achievements = 8, News = 16, Friends = 32 }

public interface IPlatformConnector
{
    string Platform { get; }                       // "steam"
    Capabilities Capabilities { get; }             // čo táto platforma reálne vie
    Task<bool> IsAvailableAsync();                 // je klient nainštalovaný?
    IAsyncEnumerable<DiscoveredGame> ScanInstalledAsync(CancellationToken ct);
    Task<IReadOnlyList<DiscoveredGame>> FetchOwnedAsync(CancellationToken ct);   // iba ak Owned
    LaunchSpec GetLaunchSpec(GameSource source);
}
```

- Jeden konektor = jeden súbor + testy na reálnych vzorkách súborov vo `fixtures/`. Zmena formátu na strane platformy rozbije jeden test, nie aplikáciu.
- Každý konektor zlyháva izolovane: chyba Epic konektora nesmie zhodiť sken Steamu.
- Ďalšie platformy (Xbox/Microsoft Store, EA app, Ubisoft Connect, Battle.net) a ručné pridanie `.exe` sú ďalšie implementácie toho istého rozhrania.

### 5.4 Vlastné sledovanie aktivity

- Každých ~5 s prejsť zoznam procesov a porovnať cestu k `.exe` s `install_path` známych hier. Funguje pre všetky platformy aj pre hry spustené mimo Gaming OS, bez admin práv.
- Používa sa len `PROCESS_QUERY_LIMITED_INFORMATION` – žiadne otváranie pamäte, bezpečné voči anti-cheatu.
- Koniec procesu = koniec relácie; nenulový návratový kód alebo záznam „Application Error“ v Event Logu = `crashed`.

### 5.5 Metadáta a obaly

| Zdroj | Použitie | Poznámka |
|---|---|---|
| Lokálna cache Steamu + Steam CDN | obaly Steam hier | bez kľúča |
| SteamGridDB | obaly pre Epic/GOG/manuálne hry | bezplatné API, kľúč používateľa |
| IGDB | žánre, dátumy, dĺžka hry, mapovanie ID medzi obchodmi (zlučovanie duplicít) | vyžaduje Twitch client secret → nesmie byť v klientovi → proxy na Cloudflare Workers (free); **podmienky pre komerčné použitie sú nejasné, pred monetizáciou si ich vyžiadať písomne** |

MVP si vystačí s prvými dvoma riadkami; zlučovanie duplicít v MVP podľa normalizovaného názvu.

---

## 6. Tímová spolupráca a viacerí Claude Code agenti

### 6.1 Princíp

Konflikty sa neriešia disciplínou, ale štruktúrou: **každý človek (a jeho agenti) vlastní moduly, moduly sa neprekrývajú v súboroch a stretávajú sa iba v kontrakte.**

### 6.2 Kde konflikty vznikajú a ako ich štruktúra ruší

| Ohnisko | Riešenie |
|---|---|
| Registrácia modulov / DI | automatické načítanie `IModule` reflexiou |
| Navigácia a routy v UI | `features/*/feature.ts` + `import.meta.glob` |
| Migrácie DB | priečinok a číslovanie za modul |
| Preklady | `features/<x>/locales/{sk,en}.json` |
| Kontrakty UI ↔ host | samostatný „contract-first“ PR, generovaný TS súbor |
| `package.json`, `.csproj`, lockfile | nové závislosti iba samostatným PR |
| Design tokeny a `kit` | jeden vlastník; ostatní žiadajú zmenu cez issue |

### 6.3 Git pravidlá

- **Trunk-based.** `main` je vždy spustiteľný. Vetvy žijú hodiny až dni: `feat/<modul>-<popis>`, `fix/<modul>-<popis>`, `contract/<modul>-<popis>`.
- **PR:** jeden modul na PR, do ~400 zmenených riadkov, squash merge, aspoň jedno schválenie človekom (nie iba agentom autora).
- **Contract-first:** zmena v `Core` kontraktoch ide ako samostatný malý PR bez implementácie. Po jeho zlúčení robia backend a UI paralelne – UI proti mocku, backend proti testom.
- **Conventional Commits** (`feat(library): …`) → changelog zadarmo.
- **Ochrana `main`:** repozitár je verejný, takže ochranu nastavíme priamo na GitHube (ruleset: zákaz priameho push, povinný PR s jedným schválením, povinné zelené CI). Pravidlo je aj v CLAUDE.md.

### 6.4 Git worktrees

```powershell
git worktree add ..\gos-library-epic -b feat/library-epic-connector
git worktree add ..\gos-ui-dashboard -b feat/activity-dashboard
```

- Jedna úloha = jeden worktree = jedna relácia Claude Code. Desktopová aplikácia Claude Code to vie robiť automaticky.
- Paralelné inštancie aplikácie si nesmú prepisovať dáta: cesta k dátam sa dá prebiť premennou `GAMINGOS_DATA_DIR` a názov single-instance mutexu ju zahŕňa.
- Po zlúčení PR worktree zmazať (`git worktree remove`).

### 6.5 CLAUDE.md a zdieľaná konfigurácia agentov

- **Koreňový `CLAUDE.md`** (krátky): čo je projekt, príkazy na build/test, hranice modulov, nemenné bezpečnostné pravidlá z 1.6, Git pravidlá, definícia hotového.
- **`CLAUDE.md` v priečinku modulu:** špecifiká modulu (formáty súborov platforiem, pasce). Načíta sa, len keď agent v module pracuje → nezaťažuje kontext ostatným.
- **`.claude/settings.json`** v repozitári: spoločné povolenia a zákazy. **`.claude/commands/`**: zdieľané postupy (`/new-connector`, `/new-feature`, `/pr`).
- Osobné veci patria do `CLAUDE.local.md` (v `.gitignore`).

Pravidlá pre agentov (pôjdu do CLAUDE.md):

1. Meň iba súbory svojho modulu. Ak treba siahnuť inam, povedz to v popise PR.
2. Kontrakt nemeň vo feature PR.
3. Nepridávaj závislosti bez samostatného PR.
4. Systémové zmeny výhradne cez katalóg tweakov a žurnál.
5. Pred PR spusti testy a typecheck; do popisu napíš, čo si overil a čo nie.
6. Žiadne tajomstvá v kóde, logoch ani fixtúrach.

### 6.6 Dokumentácia rozhraní

- Kontrakt = C# rozhrania s XML komentármi v `Core` + vygenerovaný `generated.ts`. Samostatnú API dokumentáciu nepíšeme ručne – zastarala by.
- Rozhodnutia s trvalým dopadom = krátky ADR v `docs/adr/`.

### 6.7 CI a spoločná testovacia verzia

- **PR:** `dotnet build` + `dotnet test`, `npm run typecheck && npm run lint && npm test`, kontrola čerstvosti `generated.ts`. Windows runner na GitHub Actions.
- **Merge do `main`:** build → balík cez Velopack → GitHub Release označený ako pre-release (kanál `nightly`). Tím má nainštalovaný kanál `nightly` a aplikácia sa aktualizuje sama → všetci testujú to isté.
- **Stabilné vydania:** tag `vX.Y.Z` → kanál `stable`.
- Úlohy: GitHub Issues + Projects, štítky podľa modulov.

### 6.8 Navrhované rozdelenie vlastníctva (upraví sa podľa veľkosti tímu)

| Rola | Moduly |
|---|---|
| A – Platforma | Host, Core, CI, Helper, vydania |
| B – UI | Shell, Kit, design, všetky `features/*` obrazovky |
| C – Hry a dáta | Library, Activity, Intel, Achievements |
| D – Systém a AI | System, Doctor, AI, Performance, Boost |

Pri troch ľuďoch sa zlúčia A a D.

Nikto z tímu nemá prax s C#, všetci pracujú cez Claude Code. Vlastník modulu preto nie je „expert na jazyk“, ale človek, ktorý pozná správanie modulu, ručne ho testuje a schvaľuje PR doň. Čo z toho vyplýva:

- **Popis PR je po slovensky a pre človeka, ktorý kód nečíta:** čo sa zmenilo, prečo, ako to ručne vyskúšať, čo overené nebolo.
- **Testy nahrádzajú čítanie kódu.** Netriviálna logika bez testu sa nezlučuje.
- **Prísny kompilátor:** `Nullable` zapnuté, varovania sú chyby.
- **Druhý pohľad robí agent:** pred schválením si reviewer spustí `/code-review` vo vlastnom Claude Code.

---

## 7. Technologický stack a náklady

### 7.1 Stack (všetko bezplatné)

| Vrstva | Voľba | Licencia |
|---|---|---|
| UI | React 19, TypeScript, Vite, Tailwind CSS, Motion (animácie), Zustand (stav) | MIT |
| Navigácia gamepadom | norigin-spatial-navigation | MIT |
| Host | .NET 10 LTS, WPF, WebView2 | MIT / súčasť Windows |
| DB | SQLite, Microsoft.Data.Sqlite, Dapper | public domain / MIT / Apache-2.0 |
| Inštalátor a aktualizácie | Velopack + GitHub Releases | MIT |
| Senzory (fáza 3) | LibreHardwareMonitorLib | MPL-2.0 |
| FPS a frametime (fáza 3) | Intel PresentMon | MIT |
| Lokálne AI | Ollama (inštaluje si používateľ) | MIT |
| Testy | xUnit, Vitest, Playwright (UI proti mocku) | MIT / Apache-2.0 |
| Repo, CI, issues | GitHub Free + Actions | – |
| Web, prípadná proxy | Cloudflare Pages / Workers (free tier) | – |

### 7.2 Odhad nákladov

| Položka | Do verejnej bety | Po uvedení |
|---|---|---|
| Vývojové nástroje, GitHub, CI | 0 € | 0 € |
| Servery | 0 € (žiadne nie sú) | 0 € (Cloudflare free), kým nepríde synchronizácia alebo hostované AI |
| AI | 0 € (BYOK / lokálne) | 0 €; hostované AI iba ako platená Pro funkcia |
| Doména | ~10–15 €/rok | ~10–15 €/rok |
| Podpisovanie kódu | 0 € (interné buildy nepodpisujeme) | 0 € až ~120 €/rok – pozri nižšie |
| Claude Code Pro, ChatGPT Go | už platené | – |

**Podpisovanie kódu** je jediná reálna položka. Nepodpísaný inštalátor vyvolá varovanie SmartScreen a pri aplikácii, ktorá číta procesy a registry, aj podozrenie antivírusov. Možnosti:

- Azure Artifact Signing – najlacnejšie, ale v EÚ **iba pre firmy**, nie pre fyzické osoby.
- SignPath Foundation – zadarmo pre open-source projekty (podmienky overiť).
- Distribúcia cez Microsoft Store – balík podpisuje Store (poplatky a obmedzenia pre aplikáciu s admin Helperom overiť).
- Klasický certifikát od CA – rádovo 100+ €/rok.

Rozhodnúť pred verejnou betou, nie skôr.

**Limit, ktorý nie je v eurách:** Claude Code Pro má obmedzenú kapacitu. Malé PR, moduly s vlastným CLAUDE.md a vývoj UI proti mocku nie sú len o konfliktoch – šetria aj kontext a limity.

---

## 8. Plán vývoja po etapách

Odhady platia pre 3–4 ľudí pracujúcich popri inom zamestnaní cez Claude Code Pro. Každá etapa končí nainštalovateľnou verziou.

### Etapa 0 – Základy (1–2 týždne)

- Repozitár, CLAUDE.md, `.claude/`, CI, PR šablóna.
- Host: okno + WebView2 + IPC router; generovanie TS kontraktov; `MockBridge`.
- Core: SQLite, migrácie za modul, nastavenia, event bus.
- UI: shell, design tokeny (tmavá téma, neónové akcenty), navigácia gamepadom.
- Velopack + kanál `nightly`.
- **Tri spiky, ktoré môžu zmeniť plán:** (1) plynulosť animácií a gamepad vo WebView2 na TV v 4K, (2) spoľahlivý návrat fokusu po skončení hry, (3) nástroj na generovanie TS typov.
- **Hotovo, keď:** každý člen tímu si nainštaluje nightly, prejde gamepadom prázdne menu a jeho PR prejde CI.

### Etapa 1 – MVP alfa „Knižnica“ (4–6 týždňov)

- Konektory Steam, Epic, GOG (nainštalované hry) + ručné pridanie hry.
- Knižnica: mriežka obalov, detail hry, hľadanie, filtre, obľúbené; spustenie hry.
- Sledovanie aktivity a import hracieho času zo Steamu.
- Obaly (Steam cache/CDN, SteamGridDB), Living Interface lite.
- System: prehľad hardvéru a ovládačov (iba čítanie).
- Nastavenia, autostart, SK + EN.
- **Hotovo, keď:** tím používa Gaming OS ako hlavný spôsob spúšťania hier dva týždne bez návratu k launcherom.

### Etapa 2 – „Doctor a prehľad“ (4–6 týždňov) → uzavretá alfa (10–30 ľudí)

- Doctor: zber dôkazov (Event Log, WER, logy Unreal/Unity, verzia ovládača GPU, voľné miesto), pravidlový engine, 15–20 pravidiel.
- AI modul: Ollama + BYOK, anonymizácia, cache, limit.
- Intel: novinky a patch notes zo Steamu + AI súhrny.
- My Gaming Life: dashboard (čas po dňoch/týždňoch, najhranejšie, série).
- Voliteľne: vlastnené Steam hry a achievementy s vlastným kľúčom.
- **Hotovo, keď:** Doctor správne pomenuje príčinu aspoň pri polovici reálnych pádov nahlásených testermi.

### Etapa 3 – „Výkon“ (6–8 týždňov) → verejná beta

- Helper (admin, named pipe, whitelist).
- PresentMon + senzory → Session Replay (FPS, frametime, teploty, záseky na časovej osi).
- Game Boost: 6–8 vratných tweakov, žurnál, **meranie pred/po**.
- Performance Coach v1: rady z nameraných dát („GPU 99 %, 48 FPS pri cieli 60 → zníž tiene, zapni upscaling“).
- System Control: aplikácie po štarte, úložisko.
- Podpisovanie kódu, web, opt-in hlásenie chýb.

### Etapa 4 – „Návyk“ (6–8 týždňov)

- Gaming Wrapped (mesačný/ročný, export obrázka na zdieľanie – bezplatný marketing).
- Backlog Manager, Achievement Hunter (Steam), Game Memory.
- Prispôsobiteľné widgety na dashboarde, plný Living Interface.
- Ďalšie platformy podľa dopytu.

### Etapa 5 – „Ekosystém“ (priebežne)

- Social Hub, Compatibility Radar, One-Click Setup (komunitné profily), Voice.
- Marketplace – iba ak existuje komunita, ktorá ho naplní.
- Monetizácia: Pro vrstva (hostované AI bez kľúča, synchronizácia medzi PC, rozšírený Wrapped).

---

## 9. MVP

**Cieľ MVP:** overiť, že hráč nechá Gaming OS zapnutý ako predvolené prostredie a že Doctor mu aspoň raz reálne pomôže.

| Priorita | Položka | Etapa |
|---|---|---|
| P0 | Shell na celú obrazovku, ovládanie gamepadom/klávesnicou/myšou | 0–1 |
| P0 | Knižnica: Steam + Epic + GOG nainštalované hry, spustenie, obaly | 1 |
| P0 | Sledovanie aktivity od prvého dňa | 1 |
| P0 | Inštalátor + automatické aktualizácie | 0 |
| P1 | Prehľad hardvéru a ovládačov | 1 |
| P1 | Living Interface lite, autostart, SK/EN | 1 |
| P1 | Doctor v0: detekcia pádu + pravidlá + voliteľné AI vysvetlenie | 2 |
| P1 | My Gaming Life dashboard | 2 |
| P2 | Vlastnené Steam hry, achievementy, novinky | 2 |

**Vedome mimo MVP:** Boost, senzory a FPS (vyžadujú admin práva a dôveru, ktorú si treba najprv získať), Wrapped (potrebuje mesiace dát – tie sa však už zbierajú), Social, Voice, Marketplace, One-Click Setup, Compatibility Radar, neoficiálne prihlásenia do Epic/GOG, náhrada shellu Windows, overlay v hre, vlastný backend.

---

## 10. Riziká, obmedzenia a problematické funkcie

### 10.1 Riziká

| # | Riziko | Dopad | Zmiernenie |
|---|---|---|---|
| 1 | **Konkurencia zadarmo:** Xbox mode priamo vo Windows 11, Playnite, Big Picture | launcher nikoho nepresvedčí | diferenciácia cez Doctor, merané relácie a Wrapped; jeden diferenciátor už v MVP |
| 2 | **Rozsah:** 18 funkcií, malý tím, obmedzené limity agentov | nič nebude dotiahnuté | etapy s kritériom „hotovo, keď“; zoznam „mimo MVP“ je záväzný |
| 3 | **Dáta platforiem:** Epic/GOG bez API, lokálne formáty sa môžu zmeniť, Steam kľúč nesmie byť v klientovi | diery v knižnici, rozbité konektory | príznaky `Capabilities`, izolované konektory, testy na fixtúrach, BYOK kľúč |
| 4 | **Dôvera a falošné poplachy AV:** nepodpísaná aplikácia číta procesy, registry, neskôr žiada admin | SmartScreen, Defender, odinštalovanie | žiadna injekcia, oddelený Helper, podpis pred verejnou betou, verejný katalóg tweakov |
| 5 | **Senzory vyžadujú ovládač jadra.** WinRing0 (pôvodný základ LibreHardwareMonitor) Defender od 3/2025 označuje ako zraniteľný; nástroje prechádzajú na PawnIO | bez neho nie sú teploty CPU | predvolene iba metriky GPU bez ovládača; teploty CPU ako výslovný opt-in; stav PawnIO v LHM overiť na začiatku etapy 3 |
| 6 | **„Booster“ má povesť placeba** | strata dôveryhodnosti | každý tweak sa meria pred/po cez PresentMon a ukáže skutočný rozdiel, aj nulový |
| 7 | **AI halucinuje opravy** | poškodený systém, strata dôvery | AI len vysvetľuje; opravy sú z katalógu a schvaľuje ich používateľ |
| 8 | **Réžia počas hry** (WebView2 berie RAM a GPU) | horší výkon = opak sľubu | uspanie UI počas hry, meranie vlastnej réžie v CI |
| 9 | **Licencie dát:** IGDB (komerčné použitie nejasné), PCGamingWiki (CC BY-NC-SA – nekomerčné), obaly hier v marketingu | právny problém pri monetizácii | v MVP len Steam + SteamGridDB; písomné potvrdenia pred spoplatnením |
| 10 | **Názov „Gaming OS“** je opisný (ťažko chrániteľný) a nie je to operačný systém | zámena, problém so známkou | brať ako pracovný názov; rešerš pred verejnou betou |
| 11 | **Súkromie (GDPR):** logy, zoznam hier, hardvér | právne aj reputačné | local-first, žiadna telemetria bez súhlasu, náhľad dát pred odoslaním do AI |
| 12 | **Proces s agentmi:** konflikty, rozchádzajúce sa konvencie, vyčerpané limity | spomalenie | štruktúra z kapitoly 6, malé PR, kontrakt-first |
| 13 | **Nikto z tímu nečíta C# plynulo** | ľudské review nezachytí chyby v backende | povinné testy, prísny kompilátor, zrozumiteľný popis PR s návodom na ručné overenie, `/code-review` druhým agentom (pozri 6.8) |

### 10.2 Verdikt k jednotlivým funkciám

| Funkcia | Verdikt | Prečo |
|---|---|---|
| Game Center | ✅ realizovateľné | lokálna detekcia je overená cesta |
| System Control | ✅ | čítanie jednoduché; zmeny cez Helper |
| My Gaming Life | ✅ | vlastné dáta, žiadna závislosť |
| Gaming Wrapped | ✅ (neskôr) | potrebuje nazbierané dáta; šablóny fungujú aj bez AI |
| Living Interface | ✅ | farby z obalu; lacné |
| AI Game Doctor | ✅ jadro produktu | kvalita závisí od počtu a kvality pravidiel, nie od modelu |
| Game Intelligence | ⚠️ obmedzené | spoľahlivo len Steam |
| Achievement Hunter | ⚠️ obmedzené | iba Steam má API |
| Session Replay | ⚠️ | PresentMon potrebuje admin alebo členstvo v skupine „Performance Log Users“; teploty CPU pozri riziko 5 |
| Game Boost | ⚠️ | len merateľné, vratné tweaky; žiadne „čističe registrov“ |
| Backlog Manager | ⚠️ | potrebuje dĺžku hier (IGDB – licencia) |
| AI Performance Coach | ⚠️ | neexistuje bezplatná databáza nastavení; staviame na vlastných meraniach |
| Game Memory | ⚠️ | postup vieme len odhadnúť z achievementov; riziko spoilerov a halucinácií |
| Gaming Social Hub | ❗ problematické | Steam len čítanie; Discord vyžaduje schválenie citlivých oprávnení; Epic/GOG nič; pozvánky do hier takmer nemožné |
| Compatibility Radar | ❗ | chýba zdroj dát; vyžaduje komunitu alebo vlastnú databázu |
| One-Click Game Setup | ❗ | každá hra má iný formát konfigurácie → ručná práca na hru; iba cez komunitné profily |
| Voice Assistant | ❗ nízka priorita | Windows rozpoznávanie reči má slabú podporu slovenčiny; lokálny Whisper je ďalší model na stiahnutie |
| Gaming Marketplace | ❗ posledné | backend, moderovanie, platby, bezpečnosť rozšírení; bez komunity prázdny |

---

## 11. Rozhodnutia (schválené 24. 11. 2026)

1. **Stack:** .NET host + WebView2 + React (variant C z 1.2).
2. **Tím:** 3–4 ľudia, každý s vlastným Claude Code; nikto nemá prax s C# (dôsledky v 6.8). Mená k rolám A–D treba ešte priradiť.
3. **Open-source:** áno, verejný repozitár na GitHube pod licenciou **GPL-3.0** – kto kód prevezme, musí svoje úpravy tiež zverejniť. Závislosti musia byť s GPL-3.0 kompatibilné (MIT, BSD, Apache-2.0 a MPL-2.0 sú v poriadku; proprietárne a „non-commercial“ nie).
4. **Názov:** „Gaming OS“ je pracovný. Zobrazovaný názov preto žije na jedinom mieste (prekladový kľúč `app.name`), nikde inde sa nepíše natvrdo.
5. **Rozsah MVP:** podľa kapitoly 9, bez zmien.
6. **Jazyky:** slovenčina aj angličtina od prvej verzie. Každý text v UI ide cez prekladové súbory v oboch jazykoch.

---

## Zdroje overené 24. 11. 2026

- Xbox mode vo Windows 11: [Xbox Wire](https://news.xbox.com/2026/04/30/xbox-mode-pc-windows-11/), [Windows Central](https://www.windowscentral.com/gaming/xbox/xbox-mode-is-here-microsoft-rolls-out-console-style-experience-on-windows-11-barely-meeting-its-own-deadline)
- WinRing0 a Defender: [Microsoft Support](https://support.microsoft.com/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0)
- Steam Web API: [Terms of Use](https://steamcommunity.com/dev/apiterms), [Steamworks – autentifikácia](https://partner.steamgames.com/doc/webapi_overview/auth)
- IGDB a komerčné použitie: [Twitch Developer Forums](https://discuss.dev.twitch.com/t/commercial-use-of-igdb-api/23567), [Twitch Developer Services Agreement](https://legal.twitch.com/legal/developer-agreement)
- GitHub plány: [About GitHub's plans](https://docs.github.com/en/get-started/learning-about-github/githubs-plans), [diskusia o ochrane vetiev](https://github.com/orgs/community/discussions/174419)
- Azure Artifact Signing: [Quickstart](https://learn.microsoft.com/azure/artifact-signing/quickstart)

Neoverené z webu (z mojich znalostí, potvrdiť pred použitím): presné limity GitHub Actions pre súkromné repozitáre, podmienky SignPath Foundation a Microsoft Store, stav PawnIO v LibreHardwareMonitor, správanie `GetOwnedGames` pri súkromnom profile, rozsah zlučovania knižníc v Xbox mode.
