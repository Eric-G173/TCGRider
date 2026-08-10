import React from 'react';

function useCardDragSelect(filteredCards) {
  const [selectedCardIds, setSelectedCardIds] = React.useState(new Set());
  const [dragBox, setDragBox] = React.useState(null);
  const [cardContextMenu, setCardContextMenu] = React.useState(null);

  const cardGridRef = React.useRef(null);
  const cardRefs = React.useRef({});
  const dragStartContentRef = React.useRef(null);
  const lastRawPointRef = React.useRef(null);
  const isDraggingRef = React.useRef(false);
  const additiveDragRef = React.useRef(false);

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
    if (e.target.closest('[data-context-menu]')) return;
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

  React.useEffect(() => {
    window.addEventListener('mousemove', handleDragMouseMove);
    window.addEventListener('mouseup', handleDragMouseUp);
    return () => {
      window.removeEventListener('mousemove', handleDragMouseMove);
      window.removeEventListener('mouseup', handleDragMouseUp);
    };
  }, [handleDragMouseMove, handleDragMouseUp]);

  return {
    cardGridRef,
    cardRefs,
    dragBox,
    selectedCardIds,
    setSelectedCardIds,
    cardContextMenu,
    setCardContextMenu,
    handleGridMouseDown,
    handleGridScroll,
    handleCardGridContextMenu,
  };
}

export default useCardDragSelect;