# Gaming OS (pracovný názov)

Herné prostredie pre Windows 11, ktoré beží nad systémom na celú obrazovku: jednotná knižnica hier, herné štatistiky, AI diagnostika problémov, merané optimalizácie výkonu. Nie je to operačný systém ani obchod s hrami.

Celý plán, architektúra a zdôvodnenia rozhodnutí: [docs/PLAN.md](docs/PLAN.md). Pred väčšou zmenou si prečítaj príslušnú kapitolu.

## Stav

Etapa 0. Existuje kostra UI (`src/ui`), ktorá beží v prehliadači proti ukážkovým dátam. Host (.NET) ešte neexistuje.

## Príkazy

Všetky sa spúšťajú v `src/ui`:

- `npm install` – raz po naklonovaní
- `npm run dev` – UI v prehliadači na http://localhost:5173 s ukážkovými dátami (`contracts/mockBridge.ts`)
- `npm test` – testy
- `npm run build` – typecheck + produkčný build

## Ako je UI poskladané

- `src/ui/src/shell/` – navigácia, ovládanie (klávesnica, gamepad), preklady, nastavenia, spoločné tokeny v `shell.css`.
- `src/ui/src/features/<názov>/` – jedna obrazovka. Zaregistruje sa sama súborom `feature.ts`; preklady má v `locales/sk.json` a `locales/en.json`.
- `src/ui/src/contracts/` – jediné miesto, kde UI hovorí s hostom. `api.ts` je kontrakt, `bridge.ts` vyberie skutočný host alebo ukážkové dáta.
- Ovládateľné prvky sú `FocusButton` zo `shell/Focusable.tsx`, inak sa na ne nedá dostať gamepadom.

## Stack

- Host: .NET 10, WPF okno + WebView2 (C#)
- UI: React + TypeScript + Vite, beží vo WebView2
- Dáta: SQLite lokálne u používateľa, žiadny vlastný server
- AI: pravidlá → lokálna Ollama → vlastný kľúč používateľa (BYOK)

## Tím a jazyk

- 3–4 ľudia, každý s vlastným Claude Code. Nikto z tímu nemá prax s C#.
- S ľuďmi komunikuj po slovensky a vysvetľuj bez predpokladu, že čitateľ kód prečíta.
- Kód, identifikátory, komentáre a commit správy sú po anglicky (projekt je open-source).
- Popis PR je po slovensky: čo sa zmenilo, prečo, ako to ručne vyskúšať, čo overené nebolo.

## Hranice modulov

- Modul = jeden C# projekt v `src/modules/` + jeden priečinok v `src/ui/src/features/`.
- Modul referencuje iba `Core`, nikdy iný modul. Moduly sa rozprávajú cez udalosti v `Core`.
- Meň iba súbory svojho modulu. Ak musíš siahnuť inam, napíš to do popisu PR.
- Kontrakt UI ↔ host (rozhrania a DTO v `Core`) sa mení samostatným PR `contract/...`, nikdy vo feature PR.
- Nové závislosti (NuGet, npm) iba samostatným PR.
- Migrácie DB, preklady a testovacie vzorky patria do priečinka modulu.

## Nemenné bezpečnostné pravidlá

1. Nikdy nevypínaj ani neoslabuj Windows Defender, Windows Update, firewall, UAC ani anti-cheat.
2. Žiadna injekcia do procesov hier, žiadne čítanie ich pamäte, žiadny overlay vo vnútri hry.
3. Každá zmena systému je položka z katalógu `rules/tweaks/` s `Detect / Apply / Revert`, používateľ ju vidí a schvaľuje vopred a zapíše sa do žurnálu.
4. AI iba vysvetľuje a zoraďuje opravy z katalógu. Text od modelu sa nikdy nespúšťa ako príkaz.
5. Kľúče a tokeny patria do Windows Credential Manager. Nikdy do kódu, databázy, logov ani testovacích vzoriek.
6. Do cloudovej AI neodchádza meno používateľa, cesty k profilu ani názov PC.

## Kvalita

- Netriviálna logika (vetvenie, cyklus, parser, čokoľvek so systémom) má test. Ľudia kód nečítajú, testy sú hlavná kontrola.
- C#: `Nullable` zapnuté, varovania sú chyby.
- Každý text v UI ide cez prekladové súbory `sk` aj `en`. Názov produktu je iba v kľúči `app.name`.
- Integrácie platforiem: iba lokálna detekcia a oficiálne API. Žiadny scraping, žiadne neoficiálne prihlásenia.
- Projekt je pod GPL-3.0. Závislosti musia byť kompatibilné: MIT, BSD, Apache-2.0, MPL-2.0 áno; proprietárne a „non-commercial“ nie.

## Git

- `main` je vždy spustiteľný. Nikdy doň nepushuj priamo.
- Jedna úloha = jedna vetva (ideálne vo vlastnom worktree) = jeden PR do ~400 riadkov.
- Názvy vetiev: `feat/<modul>-<popis>`, `fix/<modul>-<popis>`, `contract/<modul>-<popis>`.
- Commit správy: Conventional Commits, napr. `feat(library): add Epic connector`.
- Pred PR spusti testy a typecheck. PR schvaľuje iný človek než autor.
- Osobné poznámky patria do `CLAUDE.local.md` (nie je v gite).
