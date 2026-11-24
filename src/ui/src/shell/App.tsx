import { init, navigateByDirection, setFocus } from '@noriginmedia/norigin-spatial-navigation';
import { useEffect, useState } from 'react';
import { features } from './features';
import { FocusButton, FocusGroup } from './Focusable';
import { startGamepad } from './input';
import { useT } from './settings';

// DOM focus follows spatial focus, so :focus-visible, Enter and screen readers work as usual.
init({ shouldFocusDOMNode: true });

export function App() {
  const t = useT();
  const [activeId, setActiveId] = useState(features[0]?.id);
  const active = features.find((feature) => feature.id === activeId);

  useEffect(
    () =>
      startGamepad((action) => {
        if (action === 'confirm') (document.activeElement as HTMLElement | null)?.click();
        else if (action === 'back') setFocus('nav');
        else navigateByDirection(action, {});
      }),
    [],
  );

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setFocus('nav');
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className="shell">
      <nav className="rail" aria-label={t('app.name')}>
        <p className="rail-name">{t('app.name')}</p>
        <FocusGroup focusKey="nav" className="rail-items">
          {features.map((feature) => (
            <FocusButton
              key={feature.id}
              className="rail-item"
              aria-current={feature.id === activeId ? 'page' : undefined}
              onClick={() => setActiveId(feature.id)}
            >
              {t(feature.titleKey)}
            </FocusButton>
          ))}
        </FocusGroup>
      </nav>
      <main className="stage">
        {active && (
          <FocusGroup key={active.id} focusKey="content" autoFocus className="screen">
            <active.Screen />
          </FocusGroup>
        )}
      </main>
    </div>
  );
}
