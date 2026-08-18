import React from 'react';
import styles from './CardGrid.module.css';
import useCardDragSelect from '../../hooks/useCardDragSelect';
import CardItem from './CardItem';
import CardContextMenu from './CardContextMenu';
import CardModal from './CardModal';
import { API_BASE_URL } from '../../config';

function CardGrid({ tracker }) {
  const [cardSearch, setCardSearch] = React.useState('');
  const [cards, setCards] = React.useState([]);
  const [loadingCards, setLoadingCards] = React.useState(false);
  const [hasMissingImages, setHasMissingImages] = React.useState(false);
  const [collectedCards, setCollectedCards] = React.useState(new Set());
  const [selectedCard, setSelectedCard] = React.useState(null);

  const filteredCards = cards.filter(card =>
    card.name.toLowerCase().includes(cardSearch.toLowerCase())
  );

  const collectedCount = cards.filter(card => collectedCards.has(card.id)).length;

  const {
    cardGridRef,
    cardRefs,
    dragBox,
    selectedCardIds,
    cardContextMenu,
    setCardContextMenu,
    handleGridMouseDown,
    handleGridScroll,
    handleCardGridContextMenu,
  } = useCardDragSelect(filteredCards);

  function toggleCollected(cardId) {
    setCollectedCards(prev => {
      const updated = new Set(prev);
      if (updated.has(cardId)) {
        updated.delete(cardId);
      } else {
        updated.add(cardId);
      }
      return updated;
    });
  }

  function applyBulkCollectAction(mode) {
    setCollectedCards(prev => {
      const updated = new Set(prev);
      selectedCardIds.forEach(id => {
        if (mode === 'collect') updated.add(id);
        else if (mode === 'uncollect') updated.delete(id);
        else if (mode === 'toggle') {
          updated.has(id) ? updated.delete(id) : updated.add(id);
        }
      });
      return updated;
    });
    setCardContextMenu(null);
  }

  React.useEffect(() => {
    if (!tracker) return;

    setLoadingCards(true);
    setCards([]);

    fetch(`${API_BASE_URL}/api/cards/${tracker.setID}`)
      .then(res => res.json())
      .then(data => {
        setCards(data.cards);
        setHasMissingImages(data.hasMissingImages);
        setLoadingCards(false);
      })
      .catch(() => setLoadingCards(false));
  }, [tracker]);

  if (!tracker) return null;

  return (
    <div className={styles['tracker-view']}>

      <div className={styles['tracker-header']}>
        <h2 className={styles['tracker-title']}>
          {tracker.name}
        </h2>
        {hasMissingImages && (
          <p className={styles['tracker-disclaimer']}>
            * Cards in this set do not have images
          </p>
        )}
        <p className={styles['tracker-progress']}>
          {collectedCount} / {cards.length} cards collected
        </p>
      </div>

      <div className={styles['card-track-center']}>
        <input
          className={styles['card-search']}
          type="text"
          placeholder="Search Card..."
          value={cardSearch}
          onChange={(e) => setCardSearch(e.target.value)}
        />
      </div>

      {loadingCards ? (
        <div className={styles['tracker-empty']}>
          <p>Loading cards...</p>
        </div>
      ) : (
        <div className={styles['card-grid']}
          ref={cardGridRef}
          onMouseDown={handleGridMouseDown}
          onContextMenu={handleCardGridContextMenu}
          onScroll={handleGridScroll}
        >

          {filteredCards.map((card, i) => (
            <CardItem
              key={i}
              card={card}
              isSelected={selectedCardIds.has(card.id)}
              isCollected={collectedCards.has(card.id)}
              onToggleCollected={toggleCollected}
              onOpen={() => setSelectedCard(card)}
              cardRef={(el) => (cardRefs.current[card.id] = el)}
            />
          ))}

          {dragBox && (
            <div
              className={styles['drag-select-box']}
              style={{
                left: dragBox.x, top: dragBox.y,
                width: dragBox.w, height: dragBox.h,
              }}
            />
          )}

          {cardContextMenu && (
            <CardContextMenu
              x={cardContextMenu.x}
              y={cardContextMenu.y}
              selectedCount={selectedCardIds.size}
              onAction={applyBulkCollectAction}
              onClose={() => setCardContextMenu(null)}
            />
          )}

        </div>
      )}

      {selectedCard && (
        <CardModal
          card={selectedCard}
          isCollected={collectedCards.has(selectedCard.id)}
          onClose={() => setSelectedCard(null)}
        />
      )}
    </div>
  );
}

export default CardGrid;