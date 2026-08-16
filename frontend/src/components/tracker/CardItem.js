import React from 'react';
import styles from './CardItem.module.css';

function CardItem({ card, isSelected, isCollected, onToggleCollected, onOpen, cardRef }) {
  const [imageLoaded, setImageLoaded] = React.useState(false);

  return (
    <div
      className={`${styles['card-item']} ${isSelected ? styles['card-item-selected'] : ''}`}
      data-card-id={card.id}
      ref={cardRef}
    >
      <div className={styles['card-name']}>{card.name}</div>

      <div
        className={`${styles['card-img-wrap']} ${isCollected ? styles.collected : ''} ${!imageLoaded ? styles.loading : ''}`}
        onClick={onOpen}
      >
        <img
          className={`${styles['card-img']} ${imageLoaded ? styles.loaded : ''}`}
          src={card.imageUrl}
          alt={card.name}
          loading="lazy"
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageLoaded(true)}
        />
      </div>

      <div className={styles['card-name']} data-rarity={card.rarity}>{card.rarity}</div>
      <button
        className={`${styles['card-collect-btn']} ${isCollected ? styles['card-collect-btn-yes'] : styles['card-collect-btn-no']}`}
        onClick={(e) => {
          e.stopPropagation();
          onToggleCollected(card.id);
        }}
      >
        {isCollected ? '✓ Collected' : 'Collect'}
      </button>
    </div>
  );
}

export default CardItem;