import logo from '../logo.png';
import '../styles/Test.css';
// functions must start with capitalize letter
function Button() {
  return (
    <button>I'm a button</button>
  )
}

function Logo() {
  return (
    <img src = {logo} alt ="EmailRider" className = "logo"/>
  )
}

function AppName() {
  return (
    <p className = "AppName">EmailRider</p> //Elements must have a className in order to modify with css
  )
}
function App() {
  const { ipcRenderer } = window.require('electron');
  return (
    <div className="App">
      <header className="App-header">
        <Logo/> 
          <AppName/>
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
          {/* folders, nav links, etc */}
        </aside>
        <main className="App-content">
          {/* email list / main content */}
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
            <button className="icon-btn" aria-label="Notifications">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
              <span className="notif-dot" />
            </button>

          </div>
        </div>
      </footer>
    </div>
  );
}

export default App;