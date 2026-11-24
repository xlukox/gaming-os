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
  const [focused, setFocused] = useState<GameDto | null>(null);
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

  // Living Interface: the whole shell takes the colour of the focused game.
  useEffect(() => {
    if (focused) document.documentElement.style.setProperty('--accent', focused.accentColor);
  }, [focused]);

  if (error) return <p className="library-note">{t('library.loadFailed')}</p>;
  if (!games) return <p className="library-note">{t('library.loading')}</p>;
  if (games.length === 0) return <p className="library-note">{t('library.empty')}</p>;

  const game = focused ?? games[0];
  const launch = (target: GameDto) => {
    if (!target.installed) return;
    setLaunchedId(target.id);
    bridge.call('library.launch', { gameId: target.id }).catch(() => setLaunchedId(null));
  };

  return (
    <div className="library">
      <header className="library-hero">
        <h1>{game.title}</h1>
        <p className="library-facts">
          <span>{t(`library.platform.${game.platform}`)}</span>
          <span>
            {game.playtimeMinutes === null
              ? t('library.playtimeUnknown')
              : t('library.playtime', { hours: Math.round(game.playtimeMinutes / 60) })}
          </span>
        </p>
        <p className="library-action" aria-live="polite">
          {launchedId === game.id
            ? t('library.launching', { title: game.title })
            : game.installed
              ? t('library.pressToPlay')
              : t('library.notInstalled')}
        </p>
      </header>
      <div className="library-row">
        {games.map((item) => (
          <FocusButton
            key={item.id}
            className="cover"
            style={{ '--cover': item.accentColor } as React.CSSProperties}
            aria-label={item.title}
            aria-disabled={!item.installed}
            onClick={() => launch(item)}
            onFocused={() => setFocused(item)}
            onFocus={(event) => event.currentTarget.scrollIntoView({ inline: 'center', block: 'nearest' })}
          >
            <span>{item.title}</span>
          </FocusButton>
        ))}
      </div>
    </div>
  );
}
