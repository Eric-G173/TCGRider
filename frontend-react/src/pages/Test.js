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

function App() {
  const { ipcRenderer } = window.require('electron');
  const [search, setSearch] = React.useState('');
  const [selectedTracker, setSelectedTracker] = React.useState(null);
  const [cards, setCards] = React.useState([]);
  const [loadingCards, setLoadingCards] = React.useState(false);

  const trackers = [
    { name: "Pokémon 151", setId: "sv3pt5", completed: 0, total: 165 },
    { name: "Prismatic Evolution", setId: "sv8pt5", completed: 0, total: 193 },
  ];

  const filteredTrackers = trackers.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase())
  );

  React.useEffect(() => {
    if (selectedTracker === null) return;

    const tracker = trackers[selectedTracker];
    setLoadingCards(true);
    setCards([]);

    fetch(`http://localhost:5000/api/cards/${tracker.setId}`)
      .then(res => res.json())
      .then(data => {
        setCards(data);
        setLoadingCards(false);
      })
      .catch(() => setLoadingCards(false));
  }, [selectedTracker]);

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
          <button className="new-tracker">
            <span className="btn-plus">+</span> New Tracker
          </button>
          {filteredTrackers.map((tracker, index) => (
            <TaskBtn
              key={index}
              name={tracker.name}
              completed={tracker.completed}
              total={tracker.total}
              isSelected={selectedTracker === index}
              onClick={() => setSelectedTracker(index)}
            />
          ))}
        </aside>

        <main className="App-content">
          {selectedTracker !== null ? (
            <div className="tracker-view">
              <div className="tracker-header">
                <h2 className="tracker-title">
                  {trackers[selectedTracker].name}
                </h2>
                <p className="tracker-progress">
                  {trackers[selectedTracker].completed} / {trackers[selectedTracker].total} cards collected
                </p>
              </div>

              {loadingCards ? (
                <div className="tracker-empty">
                  <p>Loading cards...</p>
                </div>
              ) : (
                <div className="card-grid">
                  {cards.map((card, i) => (
                    <div className="card-item" key={i}>
                      <div className="card-number">#{i + 1}</div>
                      <img
                        className="card-img"
                        src={card.imageUrl}
                        alt={card.name}
                        loading="lazy"
                      />
                      <div className="card-name">{card.name}</div>
                      <button className="card-collect-btn">Collect</button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <div className="tracker-empty">
              <p>Select a tracker to view cards</p>
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