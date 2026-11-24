import { useSyncExternalStore } from 'react';
import { dicts, translate, type Lang } from './i18n';

export interface Settings {
  lang: Lang;
  scale: 100 | 125 | 150; // UI size for monitor vs. TV distance
}

// shortcut: kept in localStorage, move to the host's core_settings once the host exists.
const KEY = 'gamingos.settings';
let settings: Settings = { lang: 'sk', scale: 100, ...JSON.parse(localStorage.getItem(KEY) ?? '{}') };
const listeners = new Set<() => void>();

function apply() {
  document.documentElement.lang = settings.lang;
  document.documentElement.style.fontSize = `${settings.scale}%`;
}
apply();

export function updateSettings(patch: Partial<Settings>) {
  settings = { ...settings, ...patch };
  localStorage.setItem(KEY, JSON.stringify(settings));
  apply();
  listeners.forEach((listener) => listener());
}

export function useSettings() {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => settings,
  );
}

export function useT() {
  const { lang } = useSettings();
  return (key: string, params?: Record<string, string | number>) => translate(dicts, lang, key, params);
}
