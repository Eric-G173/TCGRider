import React from 'react';
import styles from './DefaultGrid.module.css';
import heroManifest from '../../heroCards.json';

// These names must match the game groups used in Home.jsx.
const GAMES = [
  { key: 'pokemon', name: 'Pokémon' },
  { key: 'onepiece', name: 'One Piece' },
  { key: 'yugioh', name: 'Yu-Gi-Oh' },
];

// Card art comes from `node scripts/fetch-hero-cards.mjs`, which downloads
// real cards into public/hero and lists them in src/heroCards.json.
// A game with no files (or a file that fails to load) falls back to a flat
// colored card, so the page always renders.
const toUrls = files => (files || []).map(f => `${process.env.PUBLIC_URL || ''}/hero/${f}`);

const HERO_CARDS = {
  pokemon: toUrls(heroManifest.pokemon),
  onepiece: toUrls(heroManifest.onepiece),
  yugioh: toUrls(heroManifest.yugioh),
};

const ROWS = 6;           // raise for very tall screens
const TILES_PER_ROW = 14; // raise for very wide screens
const SPEEDS = [90, 120, 100, 135, 110, 95];   // seconds per loop, per row
const PHASES = [0, 0.35, 0.6, 0.15, 0.8, 0.5]; // starting offset, per row

// Interleave the games so every row mixes all three.
function buildPool() {
  const longest = Math.max(3, ...GAMES.map(g => HERO_CARDS[g.key].length));
  const pool = [];
  for (let i = 0; i < longest; i++) {
    GAMES.forEach(g => {
      const art = HERO_CARDS[g.key];
      pool.push({ game: g.key, src: art.length ? art[i % art.length] : null });
    });
  }
  return pool;
}

const POOL = buildPool();
const ROW_TILES = Array.from({ length: ROWS }, (_, r) =>
  Array.from({ length: TILES_PER_ROW }, (_, i) => POOL[(r * 5 + i) % POOL.length])
);

const pickRandom = list => (list.length ? list[Math.floor(Math.random() * list.length)] : null);

function CardTile({ game, src }) {
  const [failed, setFailed] = React.useState(false);
  const hasArt = Boolean(src) && !failed;
  return (
    <div className={`${styles.tile} ${styles[game]} ${hasArt ? styles['tile-art'] : ''}`}>
      {hasArt && (
        <img
          src={src}
          alt=""
          loading="lazy"
          decoding="async"
          draggable="false"
          referrerPolicy="no-referrer"
          onError={() => setFailed(true)}
        />
      )}
    </div>
  );
}

function DefaultGrid({ onSelectGame, loading = {} }) {
  // One random real card per game for the three front cards.
  // Chosen once per visit to this page, so it changes when you come back.
  const [covers] = React.useState(() => ({
    pokemon: pickRandom(HERO_CARDS.pokemon),
    onepiece: pickRandom(HERO_CARDS.onepiece),
    yugioh: pickRandom(HERO_CARDS.yugioh),
  }));

  return (
    <section className={styles.hero} aria-labelledby="home-title">
      <div className={styles.wall} aria-hidden="true">
        {ROW_TILES.map((tiles, r) => {
          const speed = SPEEDS[r % SPEEDS.length];
          const phase = PHASES[r % PHASES.length];
          return (
            <div
              key={r}
              className={`${styles.row} ${r % 2 ? styles.reverse : ''}`}
              style={{ animationDuration: `${speed}s`, animationDelay: `-${speed * phase}s` }}
            >
              {/* Two identical halves so the loop is seamless. */}
              {[...tiles, ...tiles].map((tile, i) => (
                <CardTile key={i} {...tile} />
              ))}
            </div>
          );
        })}
      </div>
      <div className={styles.shade} aria-hidden="true" />

      <div className={styles.content}>
        <h1 id="home-title" className={styles.title}>
          Collect your favorite Pokémon
        </h1>
        <p className={styles.sub}>
          Pick a game, open a set, and check off every card you own.
        </p>

        <div className={styles.games}>
          {GAMES.map(game => {
            const isLoading = Boolean(loading[game.name]);
            const cover = covers[game.key];
            return (
              <button
                type="button"
                key={game.key}
                className={`${styles['game-btn']} ${styles[game.key]}`}
                data-testid={`home-game-${game.key}`}
                disabled={isLoading}
                onClick={() => onSelectGame && onSelectGame(game.name)}
              >
                {cover && (
                  <img
                    className={styles['game-art']}
                    src={cover}
                    alt=""
                    draggable="false"
                    onError={e => { e.currentTarget.style.display = 'none'; }}
                  />
                )}
                <span className={styles['game-label']}>
                  <span className={styles['game-name']}>{game.name}</span>
                  {isLoading && <span className={styles['game-note']}>Loading sets…</span>}
                </span>
              </button>
            );
          })}
        </div>

        <p className={styles.legal}>
          TCGRider is an independent fan project and isn’t affiliated with or
          endorsed by any trading card game publisher.
        </p>
      </div>
    </section>
  );
}

export default DefaultGrid;