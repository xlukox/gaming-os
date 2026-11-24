# Gaming OS (pracovný názov)

Herné prostredie pre Windows 11: jednotná knižnica hier zo Steamu, Epicu a GOG, herné štatistiky, AI diagnostika problémov a merané, vratné optimalizácie výkonu. Beží nad Windows ako aplikácia na celú obrazovku.

*A console-like gaming hub for Windows 11. Early development – nothing to install yet.*

Stav: **etapa 0** – existuje kostra rozhrania s ukážkovými dátami, zatiaľ bez napojenia na skutočné hry. Plán je v [docs/PLAN.md](docs/PLAN.md).

## Ako si to pozrieť

```
cd src/ui
npm install
npm run dev
```

Otvor http://localhost:5173. Ovláda sa šípkami a Enterom, Esc vracia do menu; funguje aj gamepad.

## Ako sa pridať do vývoja

Potrebuješ: [Git](https://git-scm.com/), [Node.js](https://nodejs.org/) 22+, [.NET SDK 10](https://dotnet.microsoft.com/download), Claude Code a účet na GitHube pridaný do tohto repozitára.

1. Naklonuj repozitár a otvor priečinok vo svojom Claude Code.
2. Claude si sám načíta [CLAUDE.md](CLAUDE.md) – spoločné pravidlá projektu platia pre všetkých rovnako.
3. Prečítaj si kapitoly 6 (spolupráca) a 9 (MVP) v pláne.

## Ako pracujeme

- Každá úloha má vlastnú vetvu a končí pull requestom. Do `main` sa priamo nepushuje.
- PR schvaľuje iný človek než autor.
- Popis PR je po slovensky a hovorí, ako zmenu ručne vyskúšať.

## Licencia

GPL-3.0.
