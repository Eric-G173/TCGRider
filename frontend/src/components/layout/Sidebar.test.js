import { render, screen, fireEvent } from '@testing-library/react';
import '@testing-library/jest-dom';
import Sidebar from './Sidebar';

const mockTrackers = [
  { name: 'Base Set', setID: 'base1', game: 'Pokémon' },
  { name: 'Romance Dawn', setID: 'OP-01', game: 'One Piece' },
];

function renderSidebar(overrides = {}) {
  const props = {
    setView: () => {},
    filteredTrackers: [],
    selectedTracker: null,
    setSelectedTracker: () => {},
    ...overrides,
  };
  return render(<Sidebar {...props} />);
}

test('renders the New Tracker button', () => {
  renderSidebar();
  expect(screen.getByText('New Tracker')).toBeInTheDocument();
});

test('clicking New Tracker switches to browse view', () => {
  const setView = jest.fn();
  renderSidebar({ setView });

  fireEvent.click(screen.getByText('New Tracker'));

  expect(setView).toHaveBeenCalledWith('browse');
});

test('renders a button for each tracker passed in', () => {
  renderSidebar({ filteredTrackers: mockTrackers });

  expect(screen.getByText('Base Set')).toBeInTheDocument();
  expect(screen.getByText('Romance Dawn')).toBeInTheDocument();
});

test('clicking a tracker selects it by index and switches to tracker view', () => {
  const setSelectedTracker = jest.fn();
  const setView = jest.fn();
  renderSidebar({ filteredTrackers: mockTrackers, setSelectedTracker, setView });

  fireEvent.click(screen.getByText('Romance Dawn'));

  expect(setSelectedTracker).toHaveBeenCalledWith(1);
  expect(setView).toHaveBeenCalledWith('tracker');
});