import React from 'react';
import styles from './CardContextMenu.module.css';

function CardContextMenu({ x, y, selectedCount, onAction, onClose }) {
  return (
    <div
      className={styles['card-context-menu']}
      data-context-menu
      style={{ left: x, top: y }}
      onMouseLeave={onClose}
    >
      <div className={styles['card-context-menu-item']} onClick={() => onAction('collect')}>
        Mark {selectedCount} as collected
      </div>
      <div className={styles['card-context-menu-item']} onClick={() => onAction('uncollect')}>
        Mark {selectedCount} as uncollected
      </div>
      <div className={styles['card-context-menu-item']} onClick={() => onAction('toggle')}>
        Toggle each
      </div>
    </div>
  );
}

export default CardContextMenu;