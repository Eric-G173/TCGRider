import React from 'react';
import styles from './CardModal.module.css';

function CardModal({ card, isCollected, onClose }) {
  return (
    <div className={styles['card-modal-overlay']} onClick={onClose}>
      <div className={`${styles['card-modal-img-wrap']} ${isCollected ? styles.collected : ''}`}>
        <img
          className={styles['card-modal-img']}
          src={card.imageUrl}
          alt={card.name}
        />
      </div>
    </div>
  );
}

export default CardModal;