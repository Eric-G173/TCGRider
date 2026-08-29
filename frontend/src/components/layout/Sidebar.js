import React from 'react';
import styles from './Sidebar.module.css';
import { API_BASE_URL } from '../../config';
import { getClientId } from '../../clientID';

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
    e.preventDefault(); // required by the browser to allow a drop to happen at all
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

      fetch(`${API_BASE_URL}/api/trackers/reorder`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          clientId: getClientId(),
          orderedSetIds: updated.map(t => t.setID),
        }),
      }).catch(err => console.error("Failed to save new order", err));

      return updated;
    });

    dragIndexRef.current = null;
  }

  return (
    <aside className={styles['App-sidebar']}>
      <div className={styles['sidebar-scroll']}>
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
              if (isEditing) return; // dragging, not navigating, while in edit mode
              setSelectedTracker(tracker.setID);
              setView('tracker');
            }}
            onDragStart={() => handleDragStart(index)}
            onDragOver={handleDragOver}
            onDrop={() => handleDrop(index)}
          />
        ))}
      </div>

      <div className={styles['sidebar-footer']}>
        <button
          className={styles['edit-trackers']}
          onClick={() => setIsEditing(prev => !prev)}
        >
          {isEditing ? 'Done' : 'Edit'}
        </button>
      </div>
    </aside>
  )
}

export default Sidebar