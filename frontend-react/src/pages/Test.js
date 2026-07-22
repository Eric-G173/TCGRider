import logo from '../logo.png';
import React from 'react';
import '../styles/Test.css';

function TaskBtn({ name, onClick, isSelected }) {
  return (
    <button 
      className={`new-btn ${isSelected ? 'new-btn-active' : ''}`} 
      onClick={onClick}
    >
      {name}
    </button>
  )
}

function Logo() {
  return (
    <img src={logo} alt="TCGRider" className="logo" />
  )
}

function AppName() {
  return (
    <p className="AppName">TCGRider</p>
  )
}
{/**/}
function App() {
    {/* the consts are react state variables. */}
  const { ipcRenderer } = window.require('electron'); {/*This connects to the electron.js file*/}
  const [search, setSearch] = React.useState('');
  const [cardSearch, setCardSearch] = React.useState('');
  const [selectedTracker, setSelectedTracker] = React.useState(null);
  const [cards, setCards] = React.useState([]);
  const [loadingCards, setLoadingCards] = React.useState(false);
const [view, setView] = React.useState('empty');
  const [collectedCards, setCollectedCards] = React.useState(new Set());

function toggleCollected(cardId) {
    setCollectedCards(prev => {
        const updated = new Set(prev);
        if (updated.has(cardId)) {
            updated.delete(cardId);
        } else {
            updated.add(cardId);
        }
        return updated;
    });
}

const [selectedCard, setSelectedCard] = React.useState(null);

  const [trackers, setTrackers] = React.useState([
  ]);

  function addTracker(newTracker) {
    setTrackers(prev => {
      if (prev.some(t => t.setID === newTracker.setID)) return prev;
      return [...prev, newTracker];
    });
  }

  
  const filteredTrackers = trackers.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase())
  );

    const filteredCards = cards.filter(card =>
    card.name.toLowerCase().includes(cardSearch.toLowerCase())
  );
const collectedCount = cards.filter(card => collectedCards.has(card.imageUrl)).length;
  React.useEffect(() => {
    if (selectedTracker === null) return;

    const tracker = trackers[selectedTracker];
    setLoadingCards(true);
    setCards([]);

    fetch(`http://localhost:5000/api/cards/${tracker.setID}`)
      .then(res => res.json())
      .then(data => {
        setCards(data);
        setLoadingCards(false);
      })
      .catch(() => setLoadingCards(false));
  }, [selectedTracker]);
const [selectedGame, setSelectedGame] = React.useState(null);
  const [availableSets, setAvailableSets] = React.useState([
  {
    game: "One Piece",
    sets: [
      { name: "Romance Dawn", setID: "op01" },
    ]
  },
  {
    game: "Topps",
    sets: [
      { name: "Match Attax", setID: "topps01" },
    ]
  }
  ]);
React.useEffect(() => {
  let cancelled = false;
  async function loadPokemonSets() {
    try {
      // request up to 250 sets in one call; adjust pageSize if needed
      const res = await fetch("http://localhost:5000/api/sets/pokemon");
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json = await res.json();
      const sets = (json.data || []).map(s => ({ name: s.name, setID: s.id }));

      if (cancelled) return;

      // merge Pokémon group into availableSets (replace or add)
      setAvailableSets(prev => {
        // remove any existing Pokémon group
        const others = prev.filter(g => g.game !== "Pokémon");
        // add the Pokémon group with the fetched sets
        return [{ game: "Pokémon", sets }, ...others];
      });
    } catch (err) {
      console.error("Failed to load Pokémon sets", err);
      // optional: setAvailableSets to include an error state or fallback list
    }
  }

  loadPokemonSets();
  return () => { cancelled = true; };
}, []);

  return (
    <div className="App">
      <header className="App-header">
        <Logo />
        <AppName />
        <div className="header-center">
          <input
            className="header-search"
            type="text"
            placeholder="Search Trackers..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="drag-region" />
        <div className="window-controls">
          <button className="btn-minimize" onClick={() => ipcRenderer.send('window-minimize')}>─</button>
          <button className="btn-maximize" aria-label="Maximize window" title="Maximize" onClick={() => ipcRenderer.send('window-maximize')}>
            <div className="maximize-icon" />
          </button>
          <button className="btn-close" onClick={() => ipcRenderer.send('window-close')}>✕</button>
        </div>
      </header>

      <div className="App-body">
  <aside className="App-sidebar">
    <button className="new-tracker" onClick={() => setView('browse')}>
      <span className="btn-plus">+</span> New Tracker
    </button>

    {filteredTrackers.map((tracker, index) => (
      <TaskBtn
        key={index}
        name={tracker.name}
        completed={tracker.completed}
        total={tracker.total}
        isSelected={selectedTracker === index}
        onClick={() => {
          setSelectedTracker(index);
          setView('tracker');
        }}
      />
    ))}
  </aside>

        <main className="App-content">
          {view === 'tracker' && selectedTracker !== null ? (
            
            <div className="tracker-view">
              
              <div className="tracker-header">
                <h2 className="tracker-title">
                  {trackers[selectedTracker].name}
                </h2>
                
                <p className="tracker-progress">
                 {collectedCount} / {cards.length} cards collected
                </p>
              </div>
<div className="card-track-center">
          <input
            className="card-search"
            type="text"
            placeholder="Search Card..."
            value={cardSearch}
            onChange={(e) => setCardSearch(e.target.value)}
          />
        </div>
              {loadingCards ? (
                <div className="tracker-empty">
                  <p>Loading cards...</p>
                </div>
              ) : (
                <div className="card-grid">
                  {filteredCards.map((card, i) => {
  const isCollected = collectedCards.has(card.imageUrl);

  return (
   <div className="card-item" key={i}>
  <div className="card-name">{card.name}</div>

  <div
    className={`card-img-wrap ${isCollected ? 'collected' : ''}`}
    onClick={() => setSelectedCard(card)}
  >
    <img
      className="card-img"
      src={card.imageUrl}
      alt={card.name}
      loading="lazy"
    />
  </div>

  <div className="card-name" data-rarity={card.rarity}>{card.rarity}</div>
  <button
    className={`card-collect-btn ${isCollected ? 'card-collect-btn-yes' : 'card-collect-btn-no'}`}
    onClick={(e) => {
      e.stopPropagation();
      toggleCollected(card.imageUrl);
    }}
  >
    {isCollected ? '✓ Collected' : 'Collect'}
  </button>
</div>
  );
})}

                </div>
              )}
            </div>
             ) : view === 'browse' ? (
 <div className="browse-view">
  {selectedGame === null ? (
    <div className="game-link-list">
      {availableSets.map((group, i) => (
        <div
          className="game-link"
          key={i}
          onClick={() => setSelectedGame(group.game)}
        >
          {group.game}
        </div>
      ))}
    </div>
  ) : (
    <div className="set-list-view">
      <button className="back-btn" onClick={() => setSelectedGame(null)}>
        ← Back
      </button>
      <h3 className="game-title">{selectedGame}</h3>
      <div className="set-list">
        {availableSets
          .find(group => group.game === selectedGame)
          .sets.map((set, j) => (
            <div className="set-row" key={j}>
        
              <button onClick={() => {
                fetch(`http://localhost:5000/api/sync/${encodeURIComponent(set.setId)}`, { method: 'POST' });
                addTracker({name: set.name, setID: set.setID});
                console.log('install setId', set.setId);
              }}
                >
              {set.name}
              </button>
            </div>
          ))}
      </div>
    </div>
  )}
</div>
          ) : (
            <div className="tracker-empty">
              <p>Select a tracker to view cards</p>
            </div>
          )}
          {selectedCard && (
    <div className="card-modal-overlay" onClick={() => setSelectedCard(null)}>
        <div className={`card-modal-img-wrap ${collectedCards.has(selectedCard.imageUrl) ? 'collected' : ''}`}>
  <img
    className="card-modal-img"
    src={selectedCard.imageUrl}
    alt={selectedCard.name}
  />
</div>

            <button
                className="card-modal-close"
                onClick={() => setSelectedCard(null)}
            >
                ✕
            </button>

    </div>
)}
        </main>
      </div>

      <footer className="App-footer">
        <div className="footer-bar">
          <div className="footer-left">
            <button className="icon-btn" aria-label="Profile">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="8" r="4" />
                <path d="M4 20c0-4 4-6 8-6s8 2 8 6" />
              </svg>
            </button>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default App;