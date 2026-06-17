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
    </div>
  );
}

export default App;
