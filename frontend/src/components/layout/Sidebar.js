import React from 'react';
import styles from './Sidebar.module.css';

function TaskBtn({ name, game, onClick, isSelected }) {
  return (
    <button
      className={`${styles['new-btn']} ${isSelected ? styles['new-btn-active'] : ''}`}
      data-game={game}
      onClick={onClick}
    >
      {name}
    </button>
  )
}

function Sidebar({ setView, filteredTrackers, selectedTracker, setSelectedTracker }) {
  return (
    <aside className={styles['App-sidebar']}>
      <button className={styles['new-tracker']} onClick={() => setView('browse')}>
        <span className={styles['btn-plus']}>+</span> New Tracker
      </button>

      {filteredTrackers.map((tracker, index) => (
        <TaskBtn
          key={index}
          name={tracker.name}
          game={tracker.game}
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
  )
}

export default Sidebar