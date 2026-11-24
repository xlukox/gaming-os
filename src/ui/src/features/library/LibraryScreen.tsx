import { setFocus } from '@noriginmedia/norigin-spatial-navigation';
import { useEffect, useState } from 'react';
import type { GameDto } from '../../contracts/api';
import { bridge } from '../../contracts/bridge';
import { FocusButton } from '../../shell/Focusable';
import { useT } from '../../shell/settings';
import './library.css';

export function LibraryScreen() {
  const t = useT();
  const [games, setGames] = useState<GameDto[] | null>(null);
  const [error, setError] = useState(false);
  const [selected, setSelected] = useState<GameDto | null>(null);
  const [launchedId, setLaunchedId] = useState<number | null>(null);

  useEffect(() => {
    const load = () => bridge.call('library.list').then(setGames, () => setError(true));
    load();
    return bridge.on('library.changed', load);
  }, []);

  // Games arrive after the screen mounts, so focus has to be placed once they exist.
  const hasGames = !!games?.length;
  useEffect(() => {
    if (hasGames) setFocus('content');
  }, [hasGames]);

  // Living Interface: the whole shell takes the colour of the selected game.
  useEffect(() => {
    if (selected) document.documentElement.style.setProperty('--accent', selected.accentColor);
  }, [selected]);

  if (error) return <p className="library-note">{t('library.loadFailed')}</p>;
  if (!games) return <p className="library-note">{t('library.loading')}</p>;
  if (games.length === 0) return <p className="library-note">{t('library.empty')}</p>;

  const game = selected ?? games[0];
  const launch = (target: GameDto) => {
    if (!target.installed) return;
    setLaunchedId(target.id);
    bridge.call('library.launch', { gameId: target.id }).catch(() => setLaunchedId(null));
  };

  return (
    <div className="library">
      <div className="library-games">
        {games.map((item) => (
          <FocusButton
            key={item.id}
            className="cover"
            style={{ '--cover': item.accentColor } as React.CSSProperties}
            aria-label={item.title}
            aria-current={item.id === game.id ? 'true' : undefined}
            aria-disabled={!item.installed}
            onClick={() => launch(item)}
            onFocused={() => setSelected(item)}
            onFocus={(event) => event.currentTarget.scrollIntoView({ block: 'nearest' })}
          >
            <span>{item.title}</span>
          </FocusButton>
        ))}
      </div>

      <section className="hero" aria-label={game.title}>
        {/* Keyed so the artwork and title fade in again for every game. */}
        <div key={game.id} className="hero-art" aria-hidden="true" />
        <div key={`content-${game.id}`} className="hero-content">
          <div className="hero-main">
            <h1>{game.title}</h1>
            <p className="hero-chips">
              <span>{t(`library.platform.${game.platform}`)}</span>
              <span>{game.installed ? t('library.installed') : t('library.notInstalled')}</span>
            </p>
            <div className="hero-actions">
              <FocusButton className="play" aria-disabled={!game.installed} onClick={() => launch(game)}>
                <svg aria-hidden="true" viewBox="0 0 24 24">
                  <path d="M6 3l15 9-15 9z" />
                </svg>
                {t('library.play')}
              </FocusButton>
              <p className="hero-status" aria-live="polite">
                {launchedId === game.id ? t('library.launching', { title: game.title }) : ''}
              </p>
            </div>
          </div>
          <p className="hero-facts">
            {game.playtimeMinutes === null
              ? t('library.playtimeUnknown')
              : t('library.playtime', { hours: Math.round(game.playtimeMinutes / 60) })}
          </p>
        </div>
      </section>
    </div>
  );
}
