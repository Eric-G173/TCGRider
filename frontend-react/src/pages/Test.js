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
    const { ipcRenderer } = window.require('electron');
  const [search, setSearch] = React.useState('');
const [cardSearch, setCardSearch] = React.useState('');
const [selectedTracker, setSelectedTracker] = React.useState(null);
const [cards, setCards] = React.useState([]);
const [loadingCards, setLoadingCards] = React.useState(false);
const [view, setView] = React.useState('empty');
const [collectedCards, setCollectedCards] = React.useState(new Set());
const [hasMissingImages, setHasMissingImages] = React.useState(false);
const [syncingSets, setSyncingSets] = React.useState(new Set());
const [selectedCardIds, setSelectedCardIds] = React.useState(new Set());
const [dragBox, setDragBox] = React.useState(null);
const [cardContextMenu, setCardContextMenu] = React.useState(null);
const [selectedCard, setSelectedCard] = React.useState(null);          // moved up
const [trackers, setTrackers] = React.useState([]);                     // moved up
const [selectedGame, setSelectedGame] = React.useState(null);           // moved up
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
  }]);

const cardGridRef = React.useRef(null);
const cardRefs = React.useRef({});
const dragStartContentRef  = React.useRef(null);
const lastRawPointRef = React.useRef(null); 
const isDraggingRef = React.useRef(false);
const additiveDragRef = React.useRef(false);

// NOW it's safe — trackers and cards both exist by this point
const filteredTrackers = trackers.filter(t =>
  t.name.toLowerCase().includes(search.toLowerCase())
);
const filteredCards = cards.filter(card =>
  card.name.toLowerCase().includes(cardSearch.toLowerCase())
);
  
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

function getRelativePoint(e) {
  const rect = cardGridRef.current.getBoundingClientRect();
  return { x: e.clientX - rect.left, y: e.clientY - rect.top };
}

function rectsIntersect(a, b) {
  return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
}
const recomputeDragSelection = React.useCallback(() => {
  const grid = cardGridRef.current;
  if (!grid || !dragStartContentRef.current || !lastRawPointRef.current) return;

  const start = dragStartContentRef.current;
  const currentContent = {
    x: lastRawPointRef.current.x + grid.scrollLeft,
    y: lastRawPointRef.current.y + grid.scrollTop,
  };

  const x = Math.min(currentContent.x, start.x);
  const y = Math.min(currentContent.y, start.y);
  const w = Math.abs(currentContent.x - start.x);
  const h = Math.abs(currentContent.y - start.y);

  // content-space box — matches absolute positioning inside the scrollable grid directly
  setDragBox({ x, y, w, h });

  // convert back to viewport space just for intersection testing against card rects
  const containerRect = grid.getBoundingClientRect();
  const boxRect = {
    left: containerRect.left + (x - grid.scrollLeft),
    top: containerRect.top + (y - grid.scrollTop),
    right: containerRect.left + (x + w - grid.scrollLeft),
    bottom: containerRect.top + (y + h - grid.scrollTop),
  };

  const intersecting = new Set();
  for (const card of filteredCards) {
    const el = cardRefs.current[card.id];
    if (!el) continue;
    if (rectsIntersect(boxRect, el.getBoundingClientRect())) intersecting.add(card.id);
  }

  setSelectedCardIds(prev => {
    const base = additiveDragRef.current ? new Set(prev) : new Set();
    intersecting.forEach(id => base.add(id));
    return base;
  });
}, [filteredCards]);

function handleGridMouseDown(e) {
  if (e.button !== 0) return;
  if (e.target.closest('[data-card-id]')) return;
  if (e.target.closest('.card-context-menu')) return;
  e.preventDefault();

  setCardContextMenu(null);
  additiveDragRef.current = e.shiftKey || e.metaKey || e.ctrlKey;
  if (!additiveDragRef.current) setSelectedCardIds(new Set());

  const grid = cardGridRef.current;
  const point = getRelativePoint(e);
  lastRawPointRef.current = point;
  dragStartContentRef.current = {
    x: point.x + grid.scrollLeft,
    y: point.y + grid.scrollTop,
  };
  isDraggingRef.current = true;
  setDragBox({ x: dragStartContentRef.current.x, y: dragStartContentRef.current.y, w: 0, h: 0 });
}
const handleDragMouseMove = React.useCallback((e) => {
  if (!isDraggingRef.current || !cardGridRef.current) return;
  lastRawPointRef.current = getRelativePoint(e);
  recomputeDragSelection();
}, [recomputeDragSelection]);

const handleDragMouseUp = React.useCallback(() => {
  isDraggingRef.current = false;
  dragStartContentRef.current = null;
  lastRawPointRef.current = null;
  setDragBox(null);
}, []);


const handleGridScroll = React.useCallback(() => {
  if (!isDraggingRef.current) return;
  recomputeDragSelection();
}, [recomputeDragSelection]);

function handleCardGridContextMenu(e) {
  e.preventDefault();
  if (selectedCardIds.size === 0) return;
  const grid = cardGridRef.current;
  const rect = grid.getBoundingClientRect();
  setCardContextMenu({
    x: e.clientX - rect.left + grid.scrollLeft,
    y: e.clientY - rect.top + grid.scrollTop,
  });
}

function applyBulkCollectAction(mode) {
  setCollectedCards(prev => {
    const updated = new Set(prev);
    selectedCardIds.forEach(id => {
      if (mode === 'collect') updated.add(id);
      else if (mode === 'uncollect') updated.delete(id);
      else if (mode === 'toggle') {
        updated.has(id) ? updated.delete(id) : updated.add(id);
      }
    });
    return updated;
  });
  setCardContextMenu(null);
}



  function addTracker(newTracker) {
    setTrackers(prev => {
      if (prev.some(t => t.setID === newTracker.setID)) return prev;
      return [...prev, newTracker];
    });
  }

  async function syncAndAddSet(set) {
  setSyncingSets(prev => new Set(prev).add(set.setID));

  try {
    const res = await fetch(`http://localhost:5000/api/sync/${encodeURIComponent(set.setID)}`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`Sync failed: HTTP ${res.status}`);

    addTracker({ name: set.name, setID: set.setID });
  } catch (err) {
    console.error(`Failed to sync set ${set.setID}`, err);
    // optionally: show an error toast/message here
  } finally {
    setSyncingSets(prev => {
      const updated = new Set(prev);
      updated.delete(set.setID);
      return updated;
    });
  }
}

const collectedCount = cards.filter(card => collectedCards.has(card.id)).length;
  React.useEffect(() => {
  if (selectedTracker === null) return;

  const tracker = trackers[selectedTracker];
  setLoadingCards(true);
  setCards([]);

  fetch(`http://localhost:5000/api/cards/${tracker.setID}`)
    .then(res => res.json())
    .then(data => {
      setCards(data.cards);
      setHasMissingImages(data.hasMissingImages);
      setLoadingCards(false);
    })
    .catch(() => setLoadingCards(false));
}, [selectedTracker]);
React.useEffect(() => {
  let cancelled = false;
  async function loadPokemonSets() {
  try {
    const res = await fetch("http://localhost:5000/api/sets/pokemon");
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const sets = await res.json(); // already [{ name, setID }, ...] — no remapping needed

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
  window.addEventListener('mousemove', handleDragMouseMove);
  window.addEventListener('mouseup', handleDragMouseUp);
  return () => {
    window.removeEventListener('mousemove', handleDragMouseMove);
    window.removeEventListener('mouseup', handleDragMouseUp);
  };
}, [handleDragMouseMove, handleDragMouseUp]);


  return (
    <div className="App">
      <header className="App-header">
         <input
            className="header-search"
            type="text"
            placeholder="Search Trackers..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        <div className="header-center">
         <Logo />
        <AppName />
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
                {hasMissingImages && (
    <p className="tracker-disclaimer">
      * Cards in this set do not have images
    </p>
  )}
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
                <div className="card-grid"
                ref={cardGridRef}
  onMouseDown={handleGridMouseDown}
  onContextMenu={handleCardGridContextMenu}
  onScroll={handleGridScroll}
                >
                  
                  {filteredCards.map((card, i) => {
const isCollected = collectedCards.has(card.id);

  return (
   <div 
   className={`card-item ${selectedCardIds.has(card.id) ? 'card-item-selected' : ''}`}
  data-card-id={card.id}
  ref={(el) => (cardRefs.current[card.id] = el)}
  key={i}>
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
      toggleCollected(card.id);
    }}
  >
    {isCollected ? '✓ Collected' : 'Collect'}
  </button>
</div>
  );
})}
{dragBox && (
  <div
    className="drag-select-box"
    style={{
      left: dragBox.x, top: dragBox.y,
      width: dragBox.w, height: dragBox.h,
    }}
  />
)}

{cardContextMenu && (
  <div
    className="card-context-menu"
    style={{ left: cardContextMenu.x, top: cardContextMenu.y }}
    onMouseLeave={() => setCardContextMenu(null)}
  >
    <div className="card-context-menu-item" onClick={() => applyBulkCollectAction('collect')}>
      Mark {selectedCardIds.size} as collected
    </div>
    <div className="card-context-menu-item" onClick={() => applyBulkCollectAction('uncollect')}>
      Mark {selectedCardIds.size} as uncollected
    </div>
    <div className="card-context-menu-item" onClick={() => applyBulkCollectAction('toggle')}>
      Toggle each
    </div>
  </div>
)}

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
      .sets.map((set, j) => {
        const isSyncing = syncingSets.has(set.setID);
        const isTracked = trackers.some(t => t.setID === set.setID);
        return (
          <div className="set-row" key={j}>
            <button className = "set-button"
               disabled={isSyncing || isTracked}
              onClick={() => syncAndAddSet(set)}
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
            <div className="tracker-empty">
              <p>Select a tracker to view cards</p>
            </div>
          )}
          {selectedCard && (
    <div className="card-modal-overlay" onClick={() => setSelectedCard(null)}>
        <div className={`card-modal-img-wrap ${collectedCards.has(selectedCard.id) ? 'collected' : ''}`}>
  <img
    className="card-modal-img"
    src={selectedCard.imageUrl}
    alt={selectedCard.name}
  />
</div>

    </div>
)}
        </main>
      </div>


    </div>
  );
}

export default App;