import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import CardItem from './CardItem';

const mockCard = {
  id: 'base1-1',
  name: 'Alakazam',
  rarity: 'Rare',
  imageUrl: 'https://assets.tcgdex.net/en/base/base1/1/low.png',
};

function renderCardItem(overrides = {}) {
  const props = {
    card: mockCard,
    isSelected: false,
    isCollected: false,
    onToggleCollected: () => {},
    onOpen: () => {},
    cardRef: () => {},
    ...overrides,
  };
  return render(<CardItem {...props} />);
}

test('renders card name and rarity', () => {
  renderCardItem();

  expect(screen.getByText('Alakazam')).toBeInTheDocument();
  expect(screen.getByText('Rare')).toBeInTheDocument();
});

test('shows "Collect" when not collected', () => {
  renderCardItem({ isCollected: false });
  expect(screen.getByText('Collect')).toBeInTheDocument();
});

test('shows "✓ Collected" when collected', () => {
  renderCardItem({ isCollected: true });
  expect(screen.getByText('Collected')).toBeInTheDocument();
});

test('clicking the collect button calls onToggleCollected with the card id', () => {
  const handleToggle = jest.fn();
  renderCardItem({ onToggleCollected: handleToggle });

  fireEvent.click(screen.getByText('Collect'));

  expect(handleToggle).toHaveBeenCalledWith('base1-1');
});

test('applies the "loaded" style once the image fires onLoad', () => {
  renderCardItem();

  const img = screen.getByAltText('Alakazam');
  expect(img.className).not.toMatch(/loaded/);

  fireEvent.load(img);

  expect(img.className).toMatch(/loaded/);
});