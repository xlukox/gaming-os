import { setFocus } from '@noriginmedia/norigin-spatial-navigation';
import { useEffect, useState } from 'react';
import type { GameDto, NewsItemDto } from '../../contracts/api';
import { bridge } from '../../contracts/bridge';
import { FocusButton } from '../../shell/Focusable';
import { useSettings, useT } from '../../shell/settings';
import './library.css';
import { playtimeLabel } from './playtime';

export function LibraryScreen() {
  const t = useT();
  const [games, setGames] = useState<GameDto[] | null>(null);
  const [error, setError] = useState(false);
  const [selected, setSelected] = useState<GameDto | null>(null);
  const [launchedId, setLaunchedId] = useState<number | null>(null);
  const [news, setNews] = useState<NewsItemDto[]>([]);
  const { lang } = useSettings();

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
  // With real artwork the blurred image provides the colour and the accent stays the brand violet;
  // without it the game's own colour does the job.
  useEffect(() => {
    if (!selected) return;
    const root = document.documentElement.style;
    if (selected.heroUrl) {
      root.setProperty('--backdrop', `url("${selected.heroUrl}")`);
      root.removeProperty('--accent');
    } else {
      root.removeProperty('--backdrop');
      root.setProperty('--accent', selected.accentColor);
    }
  }, [selected]);
  // Leaving the library must not leave a game's colours on other screens.
  useEffect(
    () => () => {
      document.documentElement.style.removeProperty('--backdrop');
      document.documentElement.style.removeProperty('--accent');
    },
    [],
  );

  // News for the selected game. The short wait keeps fast browsing from asking for every game passed.
  const gameId = selected?.id ?? games?.[0]?.id;
  useEffect(() => {
    setNews([]);
    if (gameId === undefined) return;
    let current = true;
    const timer = setTimeout(
      () =>
        bridge.call('intel.news', { gameId }).then(
          (items) => current && setNews(items),
          () => {}, // no news is not worth an error message
        ),
      350,
    );
    return () => {
      current = false;
      clearTimeout(timer);
    };
  }, [gameId]);

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
            className={item.coverUrl ? 'cover has-art' : 'cover'}
            style={
              {
                '--cover': item.coverUrl ? undefined : item.accentColor,
                backgroundImage: item.coverUrl ? `url("${item.coverUrl}")` : undefined,
              } as React.CSSProperties
            }
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
        <div
          key={game.id}
          className={game.heroUrl ? 'hero-art has-art' : 'hero-art'}
          style={game.heroUrl ? ({ '--hero': `url("${game.heroUrl}")` } as React.CSSProperties) : undefined}
          aria-hidden="true"
        />
        <div key={`content-${game.id}`} className="hero-content">
          <div className="hero-main">
            <h1>{game.logoUrl ? <img src={game.logoUrl} alt={game.title} /> : game.title}</h1>
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
          <div className="hero-side">
            {news.length > 0 && (
              <section className="news" aria-labelledby="news-title">
                <h2 id="news-title">{t('library.whatsNew')}</h2>
                <ul>
                  {news.map((item) => (
                    <li key={item.url}>
                      <span>{item.title}</span>
                      <time dateTime={item.publishedAt}>
                        {new Date(item.publishedAt).toLocaleDateString(lang, { day: 'numeric', month: 'long' })}
                      </time>
                    </li>
                  ))}
                </ul>
              </section>
            )}
            <p className="hero-facts">{t(...playtimeLabel(game.playtimeMinutes))}</p>
          </div>
        </div>
      </section>
    </div>
  );
}
