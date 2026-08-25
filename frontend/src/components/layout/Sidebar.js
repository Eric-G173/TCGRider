import React from 'react';
import styles from './Sidebar.module.css';

function TaskBtn({ name, game, onClick, isSelected, isEditing, onDragStart, onDragOver, onDrop }) {
  return (
    <button
      className={`${styles['new-btn']} ${isSelected ? styles['new-btn-active'] : ''} ${isEditing ? styles['new-btn-draggable'] : ''}`}
      data-game={game}
      draggable={isEditing}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDrop={onDrop}
      onClick={onClick}
    >
      {isEditing && <span className={styles['drag-handle']}>⠿</span>}
      {name}
    </button>
  )
}

function Sidebar({ setView, filteredTrackers, selectedTracker, setSelectedTracker, setTrackers }) {
  const [isEditing, setIsEditing] = React.useState(false);
  const dragIndexRef = React.useRef(null);

  function handleDragStart(index) {
    dragIndexRef.current = index;
  }

  function handleDragOver(e) {
    e.preventDefault();
  }

  function handleDrop(dropIndex) {
    const dragIndex = dragIndexRef.current;
    if (dragIndex === null || dragIndex === dropIndex) return;

    const draggedTracker = filteredTrackers[dragIndex];
    const targetTracker = filteredTrackers[dropIndex];

    setTrackers(prev => {
      const updated = [...prev];
      const fromRealIndex = updated.findIndex(t => t.setID === draggedTracker.setID);
      const toRealIndex = updated.findIndex(t => t.setID === targetTracker.setID);
      const [moved] = updated.splice(fromRealIndex, 1);
      updated.splice(toRealIndex, 0, moved);
      return updated;
    });

    dragIndexRef.current = null;
  }

  return (
    <aside className={styles['App-sidebar']}>
      <button className={styles['new-tracker']} onClick={() => setView('browse')}>
        <span className={styles['btn-plus']}>+</span> New Tracker
      </button>

      {filteredTrackers.map((tracker, index) => (
        <TaskBtn
          key={tracker.setID}
          name={tracker.name}
          game={tracker.game}
          isSelected={selectedTracker === tracker.setID}
          isEditing={isEditing}
          onClick={() => {
            if (isEditing) return; 
            setSelectedTracker(tracker.setID);
            setView('tracker');
          }}
          onDragStart={() => handleDragStart(index)}
          onDragOver={handleDragOver}
          onDrop={() => handleDrop(index)}
        />
      ))}

      <button
        className={styles['edit-trackers']}
        onClick={() => setIsEditing(prev => !prev)}
      >
        {isEditing ? 'Done' : 'Edit'}
      </button>
    </aside>
  )
}

export default Sidebar