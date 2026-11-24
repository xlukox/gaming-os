import { expect, test } from 'vitest';
import { buildDicts, dicts, translate } from './i18n';

const sample = buildDicts({
  './locales/sk.json': { hello: 'Ahoj {name}' },
  './locales/en.json': { hello: 'Hello {name}', onlyEn: 'English only' },
  '../features/x/locales/sk.json': { x: 'iks' },
  '../features/x/locales/readme.md': { ignored: 'yes' },
});

test('merges files per language and ignores other files', () => {
  expect(sample.sk).toEqual({ hello: 'Ahoj {name}', x: 'iks' });
  expect(sample.en).not.toHaveProperty('ignored');
});

test('fills parameters and falls back to English, then to the key', () => {
  expect(translate(sample, 'sk', 'hello', { name: 'Eva' })).toBe('Ahoj Eva');
  expect(translate(sample, 'sk', 'onlyEn')).toBe('English only');
  expect(translate(sample, 'sk', 'missing.key')).toBe('missing.key');
  expect(translate(sample, 'sk', 'hello')).toBe('Ahoj {name}');
});

// Project rule: every UI text exists in both languages.
test('Slovak and English locale files have the same keys', () => {
  expect(Object.keys(dicts.sk).sort()).toEqual(Object.keys(dicts.en).sort());
  expect(Object.keys(dicts.sk).length).toBeGreaterThan(0);
});
