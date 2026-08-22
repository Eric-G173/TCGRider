function filterCards(cards, { search = '', rarity = 'All', collectedFilter = 'All', collectedIds = new Set() } = {}) {
  const normalizedSearch = (search || '').toLowerCase();

  return (cards || []).filter(card => {
    const matchesSearch = card.name.toLowerCase().includes(normalizedSearch);
    const matchesRarity = rarity === 'All' || card.rarity === rarity;

    const isCollected = collectedIds.has(card.id);
    const matchesCollected =
      collectedFilter === 'All' ||
      (collectedFilter === 'Collected' && isCollected) ||
      (collectedFilter === 'Uncollected' && !isCollected);

    return matchesSearch && matchesRarity && matchesCollected;
  });
}

export default filterCards;
