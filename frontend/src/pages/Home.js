import React from 'react';
import styles from './Home.module.css';
import TopBar from '../components/layout/TopBar';
import Sidebar from '../components/layout/Sidebar';
import DefaultGrid from '../components/tracker/DefaultGrid';
import CardGrid from '../components/tracker/CardGrid';
import { API_BASE_URL } from '../config';

function App() {
  const [search, setSearch] = React.useState('');
  const [selectedTracker, setSelectedTracker] = React.useState(null);
  const [view, setView] = React.useState('empty');
  const [syncingSets, setSyncingSets] = React.useState(new Set());
  const [trackers, setTrackers] = React.useState([]);
  const [selectedGame, setSelectedGame] = React.useState(null);
  const [gameTrackerLoaded, setGameTrackerLoaded] = React.useState(false);
  const [availableSets, setAvailableSets] = React.useState([
   ]);

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
      ? `${API_BASE_URL}/api/sync/onepiece/${encodeURIComponent(set.setID)}`
      : `${API_BASE_URL}/api/sync/${encodeURIComponent(set.setID)}`;

    try {
      const res = await fetch(endpoint, { method: 'POST' });
      if (!res.ok) throw new Error(`Sync failed: HTTP ${res.status}`);

      const data = await res.json();
      if (!data.hasCards) {
        console.warn(`Set ${set.setID} has no card data — not adding as tracker`);
        return;
      }

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


    async function loadPokemonSets() {
  const res = await fetch(`${API_BASE_URL}/api/sets/pokemon`);
  const sets = await res.json();

  setAvailableSets(prev => {
    const others = prev.filter(g => g.game !== "Pokémon");
    return [{ game: "Pokémon", sets }, ...others];
  });
}

async function loadOnePieceSets() {
  const res = await fetch(`${API_BASE_URL}/api/sets/onepiece`);
  const sets = await res.json();

  setAvailableSets(prev => {
    const others = prev.filter(g => g.game !== "One Piece");
    return [{ game: "One Piece", sets }, ...others];
  });
}


  React.useEffect(() => {
  async function loadAll() {
    await Promise.all([
      loadPokemonSets(),
      loadOnePieceSets()
    ]);

    setGameTrackerLoaded(true); 
  }

  loadAll();
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
                <div className={styles['game-link-list']} >
                  {availableSets.map((group, i) => (
                    <div
                      className={`${styles['game-link']} ${!gameTrackerLoaded ? styles.loading : ''}`}
                      key={i}
                      onClick={() => setSelectedGame(group.game)}>
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