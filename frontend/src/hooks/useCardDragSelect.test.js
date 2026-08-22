import { renderHook, act } from '@testing-library/react';
import useCardDragSelect from './useCardDragSelect';

const mockCards = [{ id: 'card-1' }, { id: 'card-2' }];

// Simulates a 1000x600 grid container starting at (0,0) — enough to test
// both "near the edge" and "plenty of room" scenarios without a real browser.
function attachFakeGrid(result) {
  result.current.cardGridRef.current = {
    getBoundingClientRect: () => ({ left: 0, top: 0, right: 1000, bottom: 600 }),
    clientWidth: 1000,
    clientHeight: 600,
    scrollLeft: 0,
    scrollTop: 0,
  };
}

test('context menu flips leftward when opened near the right edge', () => {
  const { result } = renderHook(() => useCardDragSelect(mockCards));
  attachFakeGrid(result);

  act(() => {
    result.current.setSelectedCardIds(new Set(['card-1'])); // menu only opens with a selection
  });

  act(() => {
    // Click at x=950 in a 1000px-wide grid — a ~200px menu opening rightward
    // here would extend to 1150, well past the visible edge.
    result.current.handleCardGridContextMenu({
      preventDefault: () => {},
      clientX: 950,
      clientY: 100,
    });
  });

  expect(result.current.cardContextMenu.x).toBeLessThan(950);
  expect(result.current.cardContextMenu.x + 200).toBeLessThanOrEqual(1000);
});

test('context menu does not flip when there is plenty of room', () => {
  const { result } = renderHook(() => useCardDragSelect(mockCards));
  attachFakeGrid(result);

  act(() => {
    result.current.setSelectedCardIds(new Set(['card-1']));
  });

  act(() => {
    // Click at x=300 — a 200px menu opening rightward only reaches 500,
    // nowhere near the 1000px edge, so no flip should happen.
    result.current.handleCardGridContextMenu({
      preventDefault: () => {},
      clientX: 300,
      clientY: 100,
    });
  });

  expect(result.current.cardContextMenu.x).toBe(300);
});