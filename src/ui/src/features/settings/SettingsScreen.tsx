import { FocusButton } from '../../shell/Focusable';
import { updateSettings, useSettings, useT, type Settings } from '../../shell/settings';
import './settings.css';

const LANGS: { value: Settings['lang']; label: string }[] = [
  { value: 'sk', label: 'Slovenčina' },
  { value: 'en', label: 'English' },
];
const SCALES: Settings['scale'][] = [100, 125, 150];

export function SettingsScreen() {
  const t = useT();
  const settings = useSettings();

  return (
    <div className="settings">
      <h1>{t('settings.title')}</h1>

      <section aria-labelledby="settings-lang">
        <h2 id="settings-lang">{t('settings.language')}</h2>
        <div className="settings-options">
          {LANGS.map(({ value, label }) => (
            <FocusButton
              key={value}
              className="option"
              aria-pressed={settings.lang === value}
              onClick={() => updateSettings({ lang: value })}
            >
              {label}
            </FocusButton>
          ))}
        </div>
      </section>

      <section aria-labelledby="settings-scale">
        <h2 id="settings-scale">{t('settings.scale')}</h2>
        <p>{t('settings.scaleHint')}</p>
        <div className="settings-options">
          {SCALES.map((scale) => (
            <FocusButton
              key={scale}
              className="option"
              aria-pressed={settings.scale === scale}
              onClick={() => updateSettings({ scale })}
            >
              {scale} %
            </FocusButton>
          ))}
        </div>
      </section>
    </div>
  );
}
