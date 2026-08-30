import React from 'react';
import styles from './CardModal.module.css';
import { API_BASE_URL } from '../../config';

function CardModal({ card, isCollected, onClose }) {
  const resolvedImageUrl = card.imageUrl?.startsWith('/')
    ? `${API_BASE_URL}${card.imageUrl}`
    : card.imageUrl;

  return (
    <div className={styles['card-modal-overlay']} onClick={onClose}>
      <div className={`${styles['card-modal-img-wrap']} ${isCollected ? styles.collected : ''}`}>
        <img
          className={styles['card-modal-img']}
          src={resolvedImageUrl}
          alt={card.name}
        />
      </div>
    </div>
  );
}

export default CardModal;