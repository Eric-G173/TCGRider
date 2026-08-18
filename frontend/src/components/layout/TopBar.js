import logo from '../../logo.png'
import React from 'react';
import styles from './TopBar.module.css';

const isElectron = typeof window !== 'undefined' && !!window.require;
const ipcRenderer = isElectron ? window.require('electron').ipcRenderer : null;

function TopBar({search, setSearch}) {
return (
      <header className={styles['App-header']}>
        <input
          className={styles['header-search']}
          type="text"
          placeholder="Search Trackers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <div className={styles['header-center']}>
          <img src={logo} alt="TCGRider" className={styles.logo} />
          <p className={styles.AppName}>TCGRider</p>
        </div>
        <div className={styles['drag-region']} />
        {isElectron && (
          <div className={styles['window-controls']}>
            <button className={styles['btn-minimize']} onClick={() => ipcRenderer.send('window-minimize')}>─</button>
            <button className={styles['btn-maximize']} aria-label="Maximize window" title="Maximize" onClick={() => ipcRenderer.send('window-maximize')}>
              <div className={styles['maximize-icon']} />
            </button>
            <button className={styles['btn-close']} onClick={() => ipcRenderer.send('window-close')}>✕</button>
          </div>
        )}
      </header>
)
}
      export default TopBar;