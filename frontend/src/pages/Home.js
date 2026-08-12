import React from 'react';
import styles from './Home.module.css';
import TopBar from '../components/layout/TopBar';
import Sidebar from '../components/layout/Sidebar';
import DefaultGrid from '../components/tracker/DefaultGrid';
import CardGrid from '../components/tracker/CardGrid';

function App() {
  const [search, setSearch] = React.useState('');
  const [selectedTracker, setSelectedTracker] = React.useState(null);
  const [view, setView] = React.useState('empty');
  const [syncingSets, setSyncingSets] = React.useState(new Set());
  const [trackers, setTrackers] = React.useState([]);
  const [selectedGame, setSelectedGame] = React.useState(null);
  const [availableSets, setAvailableSets] = React.useState([
    {
      game: "Topps",
      sets: [
        { name: "Match Attax", setID: "topps01" },
      ]
    }]);

  const filteredTrackers = trackers.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase())
  );

  function addTracker(newTracker) {
    setTrackers(prev => {
      if (prev.some(t => t.setID === newTracker.setID)) return prev;
      return [...prev, newTracker];
    });
  }

  async function syncAndAddSet(set, game) {
    setSyncingSets(prev => new Set(prev).add(set.setID));

    // Each game's card data lives behind a different sync endpoint —
    // route based on which game group this set came from.
    const endpoint = game === "One Piece"
      ? `http://localhost:5000/api/sync/onepiece/${encodeURIComponent(set.setID)}`
      : `http://localhost:5000/api/sync/${encodeURIComponent(set.setID)}`;

    try {
      const res = await fetch(endpoint, { method: 'POST' });
      if (!res.ok) throw new Error(`Sync failed: HTTP ${res.status}`);

      addTracker({ name: set.name, setID: set.setID, game });
    } catch (err) {
      console.error(`Failed to sync set ${set.setID}`, err);
    } finally {
      setSyncingSets(prev => {
        const updated = new Set(prev);
        updated.delete(set.setID);
        return updated;
      });
    }
  }

  React.useEffect(() => {
    let cancelled = false;
    async function loadPokemonSets() {
      try {
        const res = await fetch("http://localhost:5000/api/sets/pokemon");
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const sets = await res.json();

        if (cancelled) return;

        setAvailableSets(prev => {
          const others = prev.filter(g => g.game !== "Pokémon");
          return [{ game: "Pokémon", sets }, ...others];
        });
      } catch (err) {
        console.error("Failed to load Pokémon sets", err);
      }
    }

    loadPokemonSets();
    return () => { cancelled = true; };
  }, []);

  React.useEffect(() => {
    let cancelled = false;
    async function loadOnePieceSets() {
      try {
        const res = await fetch("http://localhost:5000/api/sets/onepiece");
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const sets = await res.json();

        if (cancelled) return;

        setAvailableSets(prev => {
          const others = prev.filter(g => g.game !== "One Piece");
          return [{ game: "One Piece", sets }, ...others];
        });
      } catch (err) {
        console.error("Failed to load One Piece sets", err);
      }
    }

    loadOnePieceSets();
    return () => { cancelled = true; };
  }, []);

  return (
    <div className={styles.App}>
      <TopBar search={search} setSearch={setSearch} />

      <div className={styles['App-body']}>
        <Sidebar setView={setView} filteredTrackers={filteredTrackers} selectedTracker={selectedTracker} setSelectedTracker={setSelectedTracker} />

        <main className={styles['App-content']}>
          {view === 'tracker' && selectedTracker !== null ? (
            <CardGrid tracker={trackers[selectedTracker]} />
          ) : view === 'browse' ? (
            <div className={styles['browse-view']}>
              {selectedGame === null ? (
                <div className={styles['game-link-list']}>
                  {availableSets.map((group, i) => (
                    <div
                      className={styles['game-link']}
                      key={i}
                      onClick={() => setSelectedGame(group.game)}
                    >
                      {group.game}
                    </div>
                  ))}
                </div>
              ) : (
                <div className={styles['set-list-view']}>
                  <button className={styles['back-btn']} onClick={() => setSelectedGame(null)}>
                    ← Back
                  </button>
                  <h3 className={styles['game-title']}>{selectedGame}</h3>
                  <div className={styles['set-list']}>
                    {availableSets
                      .find(group => group.game === selectedGame)
                      .sets.map((set, j) => {
                        const isSyncing = syncingSets.has(set.setID);
                        const isTracked = trackers.some(t => t.setID === set.setID);
                        return (
                          <div className={styles['set-row']} key={j}>
                            <button className={styles['set-button']}
                              disabled={isSyncing || isTracked}
                              onClick={() => syncAndAddSet(set, selectedGame)}
                            >
                              {isSyncing ? `Syncing ${set.name}...` : set.name}
                            </button>
                          </div>
                        );
                      })}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <DefaultGrid />
          )}
        </main>
      </div>

    </div>
  );
}

export default App;