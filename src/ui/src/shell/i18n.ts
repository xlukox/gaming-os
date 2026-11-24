export type Lang = 'sk' | 'en';
export type Dict = Record<string, string>;
export type Dicts = Record<Lang, Dict>;

/** Merges locale files (shell + every feature) into one dictionary per language, keyed by file name. */
export function buildDicts(files: Record<string, Dict>): Dicts {
  const dicts: Dicts = { sk: {}, en: {} };
  for (const [path, dict] of Object.entries(files)) {
    const lang = path.match(/\/(sk|en)\.json$/)?.[1] as Lang | undefined;
    if (lang) Object.assign(dicts[lang], dict);
  }
  return dicts;
}

/** Falls back to English, then to the key itself, so a missing text is visible but never crashes. */
export function translate(dicts: Dicts, lang: Lang, key: string, params: Record<string, string | number> = {}) {
  const text = dicts[lang][key] ?? dicts.en[key] ?? key;
  return text.replace(/\{(\w+)\}/g, (whole, name) => String(params[name] ?? whole));
}

export const dicts = buildDicts(
  import.meta.glob<Dict>(['./locales/*.json', '../features/*/locales/*.json'], { eager: true, import: 'default' }),
);
