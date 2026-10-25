// Downloads a small set of real cards for the front-page wall.
//
// Run once from the frontend folder:
//   npm i -D sharp
//   node scripts/fetch-hero-cards.mjs
//
// It saves images to public/hero/ and lists them in src/heroCards.json.
// Images are self-hosted on purpose: YGOPRODeck prohibits hotlinking its
// card images, and local files also work offline in the Electron build.
// Re-run any time you want a different mix (Pokémon picks are random).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outDir = path.join(root, 'public', 'hero');
const manifestPath = path.join(root, 'src', 'heroCards.json');

// ---- What to fetch (edit freely) ----

// One random card is picked for each name.
const POKEMON_NAMES = [
  'Pikachu', 'Charizard', 'Mewtwo', 'Eevee', 'Gengar',
  'Lucario', 'Umbreon', 'Rayquaza', 'Snorlax', 'Mew',
];

const YUGIOH_NAMES = [
  'Dark Magician',
  'Blue-Eyes White Dragon',
  'Red-Eyes Black Dragon',
  'Dark Magician Girl',
  'Summoned Skull',
  'Exodia the Forbidden One',
  'Slifer the Sky Dragon',
  'Obelisk the Tormentor',
  'Kuriboh',
  'Mirror Force',
];

// Any One Piece card ID works, e.g. 'OP02-001'.
const ONEPIECE_IDS = [
  'OP01-001', 'OP01-003', 'OP01-005', 'OP01-013',
  'OP01-016', 'OP01-025', 'OP01-060',
];
// -------------------------------------

const HEADERS = { 'User-Agent': 'TCGRider-portfolio-project (one-time hero image fetch)' };
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

// sharp is optional: it shrinks big PNG/JPG files into small WebP files.
let sharp = null;
try {
  sharp = (await import('sharp')).default;
} catch {
  console.warn('sharp not installed: images will be saved at original size.');
}

async function download(url) {
  const res = await fetch(url, { headers: HEADERS });
  if (!res.ok) throw new Error(`HTTP ${res.status} for ${url}`);
  return Buffer.from(await res.arrayBuffer());
}

async function save(buffer, game, n, ext) {
  let data = buffer;
  let outExt = ext;
  if (sharp && ext !== 'webp') {
    data = await sharp(buffer).resize({ width: 264 }).webp({ quality: 80 }).toBuffer();
    outExt = 'webp';
  }
  const file = `${game}-${String(n).padStart(2, '0')}.${outExt}`;
  fs.writeFileSync(path.join(outDir, file), data);
  return file;
}

async function fetchPokemon() {
  const files = [];
  for (const name of POKEMON_NAMES) {
    try {
      // TCGdex returns brief cards: { id, localId, name, image }, where
      // `image` is a base URL and the file is `${image}/low.webp`.
      const api = 'https://api.tcgdex.net/v2/en/cards'
        + `?name=${encodeURIComponent(name)}`
        + '&pagination:page=1&pagination:itemsPerPage=50';
      const res = await fetch(api, { headers: HEADERS });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const withArt = (await res.json()).filter(card => card.image);
      if (!withArt.length) throw new Error('no cards with art');
      const pick = withArt[Math.floor(Math.random() * withArt.length)];
      const buffer = await download(`${pick.image}/low.webp`);
      files.push(await save(buffer, 'pokemon', files.length + 1, 'webp'));
    } catch (err) {
      console.warn(`  skipped ${name}: ${err.message}`);
    }
    await sleep(300);
  }
  return files;
}

async function fetchYugioh() {
  const files = [];
  for (const name of YUGIOH_NAMES) {
    try {
      const api = `https://db.ygoprodeck.com/api/v7/cardinfo.php?name=${encodeURIComponent(name)}`;
      const res = await fetch(api, { headers: HEADERS });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json = await res.json();
      const img = json.data?.[0]?.card_images?.[0]?.image_url_small;
      if (!img) throw new Error('no image in response');
      files.push(await save(await download(img), 'yugioh', files.length + 1, 'jpg'));
    } catch (err) {
      console.warn(`  skipped ${name}: ${err.message}`);
    }
    await sleep(400); // stay far below the API's rate limit
  }
  return files;
}

async function fetchOnePiece() {
  const files = [];
  for (const id of ONEPIECE_IDS) {
    try {
      const buffer = await download(`https://en.onepiece-cardgame.com/images/cardlist/card/${id}.png`);
      files.push(await save(buffer, 'onepiece', files.length + 1, 'png'));
    } catch (err) {
      console.warn(`  skipped ${id}: ${err.message}`);
    }
    await sleep(400);
  }
  return files;
}

fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(outDir, { recursive: true });

console.log('Fetching Pokémon cards...');
const pokemon = await fetchPokemon();
console.log('Fetching Yu-Gi-Oh cards...');
const yugioh = await fetchYugioh();
console.log('Fetching One Piece cards...');
const onepiece = await fetchOnePiece();

fs.writeFileSync(manifestPath, JSON.stringify({ pokemon, onepiece, yugioh }, null, 2) + '\n');
console.log(`Done: ${pokemon.length} Pokémon, ${onepiece.length} One Piece, ${yugioh.length} Yu-Gi-Oh.`);
console.log(`Images: ${outDir}`);
console.log(`Manifest: ${manifestPath}`);