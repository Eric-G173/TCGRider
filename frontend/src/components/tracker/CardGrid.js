import React from "react";
import styles from './CardGrid.module.css';

function CardGrid({ tracker }) {
    const [cardSearch, setCardSearch] = React.useState('');
  const [cards, setCards] = React.useState([]);
  const [loadingCards, setLoadingCards] = React.useState(false);
  const [hasMissingImages, setHasMissingImages] = React.useState(false);
  const [collectedCards, setCollectedCards] = React.useState(new Set());
  const [selectedCardIds, setSelectedCardIds] = React.useState(new Set());
  const [dragBox, setDragBox] = React.useState(null);
  const [cardContextMenu, setCardContextMenu] = React.useState(null);
  const [selectedCard, setSelectedCard] = React.useState(null);
const cardGridRef = React.useRef(null);
  const cardRefs = React.useRef({});
  const dragStartContentRef = React.useRef(null);
  const lastRawPointRef = React.useRef(null);
  const isDraggingRef = React.useRef(false);
  const additiveDragRef = React.useRef(false);
 
  const filteredCards = cards.filter(card =>
    card.name.toLowerCase().includes(cardSearch.toLowerCase())
  );
 
  const collectedCount = cards.filter(card => collectedCards.has(card.id)).length;
 
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
 
  function getRelativePoint(e) {
    const rect = cardGridRef.current.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }
 
  function rectsIntersect(a, b) {
    return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
  }
 
  const recomputeDragSelection = React.useCallback(() => {
    const grid = cardGridRef.current;
    if (!grid || !dragStartContentRef.current || !lastRawPointRef.current) return;
 
    const start = dragStartContentRef.current;
    const currentContent = {
      x: lastRawPointRef.current.x + grid.scrollLeft,
      y: lastRawPointRef.current.y + grid.scrollTop,
    };
 
    const x = Math.min(currentContent.x, start.x);
    const y = Math.min(currentContent.y, start.y);
    const w = Math.abs(currentContent.x - start.x);
    const h = Math.abs(currentContent.y - start.y);
 
    setDragBox({ x, y, w, h });
 
    const containerRect = grid.getBoundingClientRect();
    const boxRect = {
      left: containerRect.left + (x - grid.scrollLeft),
      top: containerRect.top + (y - grid.scrollTop),
      right: containerRect.left + (x + w - grid.scrollLeft),
      bottom: containerRect.top + (y + h - grid.scrollTop),
    };
 
    const intersecting = new Set();
    for (const card of filteredCards) {
      const el = cardRefs.current[card.id];
      if (!el) continue;
      if (rectsIntersect(boxRect, el.getBoundingClientRect())) intersecting.add(card.id);
    }
 
    setSelectedCardIds(prev => {
      const base = additiveDragRef.current ? new Set(prev) : new Set();
      intersecting.forEach(id => base.add(id));
      return base;
    });
  }, [filteredCards]);
 
  function handleGridMouseDown(e) {
    if (e.button !== 0) return;
    if (e.target.closest('[data-card-id]')) return;
    if (e.target.closest(`.${styles['card-context-menu']}`)) return;
    e.preventDefault();
 
    setCardContextMenu(null);
    additiveDragRef.current = e.shiftKey || e.metaKey || e.ctrlKey;
    if (!additiveDragRef.current) setSelectedCardIds(new Set());
 
    const grid = cardGridRef.current;
    const point = getRelativePoint(e);
    lastRawPointRef.current = point;
    dragStartContentRef.current = {
      x: point.x + grid.scrollLeft,
      y: point.y + grid.scrollTop,
    };
    isDraggingRef.current = true;
    setDragBox({ x: dragStartContentRef.current.x, y: dragStartContentRef.current.y, w: 0, h: 0 });
  }
 
  const handleDragMouseMove = React.useCallback((e) => {
    if (!isDraggingRef.current || !cardGridRef.current) return;
    lastRawPointRef.current = getRelativePoint(e);
    recomputeDragSelection();
  }, [recomputeDragSelection]);
 
  const handleDragMouseUp = React.useCallback(() => {
    isDraggingRef.current = false;
    dragStartContentRef.current = null;
    lastRawPointRef.current = null;
    setDragBox(null);
  }, []);
 
  const handleGridScroll = React.useCallback(() => {
    if (!isDraggingRef.current) return;
    recomputeDragSelection();
  }, [recomputeDragSelection]);
 
  function handleCardGridContextMenu(e) {
    e.preventDefault();
    if (selectedCardIds.size === 0) return;
    const grid = cardGridRef.current;
    const rect = grid.getBoundingClientRect();
    setCardContextMenu({
      x: e.clientX - rect.left + grid.scrollLeft,
      y: e.clientY - rect.top + grid.scrollTop,
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
 
    fetch(`http://localhost:5000/api/cards/${tracker.setID}`)
      .then(res => res.json())
      .then(data => {
        setCards(data.cards);
        setHasMissingImages(data.hasMissingImages);
        setLoadingCards(false);
      })
      .catch(() => setLoadingCards(false));
  }, [tracker]);
 
  React.useEffect(() => {
    window.addEventListener('mousemove', handleDragMouseMove);
    window.addEventListener('mouseup', handleDragMouseUp);
    return () => {
      window.removeEventListener('mousemove', handleDragMouseMove);
      window.removeEventListener('mouseup', handleDragMouseUp);
    };
  }, [handleDragMouseMove, handleDragMouseUp]);
 
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
 
          {filteredCards.map((card, i) => {
            const isCollected = collectedCards.has(card.id);
 
            return (
              <div
                className={`${styles['card-item']} ${selectedCardIds.has(card.id) ? styles['card-item-selected'] : ''}`}
                data-card-id={card.id}
                ref={(el) => (cardRefs.current[card.id] = el)}
                key={i}>
                <div className={styles['card-name']}>{card.name}</div>
 
                <div
                  className={`${styles['card-img-wrap']} ${isCollected ? styles.collected : ''}`}
                  onClick={() => setSelectedCard(card)}
                >
                  <img
                    className={styles['card-img']}
                    src={card.imageUrl}
                    alt={card.name}
                    loading="lazy"
                  />
                </div>
 
                <div className={styles['card-name']} data-rarity={card.rarity}>{card.rarity}</div>
                <button
                  className={`${styles['card-collect-btn']} ${isCollected ? styles['card-collect-btn-yes'] : styles['card-collect-btn-no']}`}
                  onClick={(e) => {
                    e.stopPropagation();
                    toggleCollected(card.id);
                  }}
                >
                  {isCollected ? '✓ Collected' : 'Collect'}
                </button>
              </div>
            );
          })}
 
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
            <div
              className={styles['card-context-menu']}
              style={{ left: cardContextMenu.x, top: cardContextMenu.y }}
              onMouseLeave={() => setCardContextMenu(null)}
            >
              <div className={styles['card-context-menu-item']} onClick={() => applyBulkCollectAction('collect')}>
                Mark {selectedCardIds.size} as collected
              </div>
              <div className={styles['card-context-menu-item']} onClick={() => applyBulkCollectAction('uncollect')}>
                Mark {selectedCardIds.size} as uncollected
              </div>
              <div className={styles['card-context-menu-item']} onClick={() => applyBulkCollectAction('toggle')}>
                Toggle each
              </div>
            </div>
          )}
 
        </div>
      )}
 
      {selectedCard && (
        <div className={styles['card-modal-overlay']} onClick={() => setSelectedCard(null)}>
          <div className={`${styles['card-modal-img-wrap']} ${collectedCards.has(selectedCard.id) ? styles.collected : ''}`}>
            <img
              className={styles['card-modal-img']}
              src={selectedCard.imageUrl}
              alt={selectedCard.name}
            />
          </div>
        </div>
      )}
    </div>
  );
}

export default CardGrid