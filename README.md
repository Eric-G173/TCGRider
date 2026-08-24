# TCGRider

A desktop app for tracking trading card game collections. Browse sets, view card details, and sync card data locally for fast, offline browsing.

**Live Demo:** https://tcgrider.onrender.com/

# Features


https://github.com/user-attachments/assets/b6c0ce2c-c2a6-4318-ac48-99352a6b1e1e


- Browse trading card game sets (currently Pokémon, One Piece, etc.)
- View all cards in a set, including rarity and card images
- Sync a set's card data into a local SQLite database with one click
- Runs as a desktop app (Electron) with a React frontend and C# backend

https://github.com/user-attachments/assets/2772eb58-345a-4e0e-9df4-85d46d3b137b

- Keep track of your collection with card collection features


https://github.com/user-attachments/assets/8553a56a-622b-45ff-bf0d-a6602ed41456







# Tech stack
- **Backend:** C# (ASP.NET Core, Minimal APIs), SQLite
- **Frontend:** React, Electron
- **APIs:** TCGdex (Pokémon set and card data)
# Architecture: why data is synced locally instead of queried live

Card lists, rarities, and images don't change often, so hitting the TCGdex API on every request to browse a set would be slow and add an unnecessary external dependency.

The fix:

POST /api/sync/{setId} pulls a set's full card list from TCGdex once and writes it into a local SQLite database (tcgrider.db).
GET /api/cards/{setId} then reads from that local database instead of calling TCGdex again.

This makes browsing near-instant after the first sync, and means previously-synced sets stay browsable even if TCGdex is down or you're offline.

# Running locally

### Backend
```
git clone https://github.com/Eric-G173/PROJECT-MIDNIGHT-RIDER.git
cd PROJECT-MIDNIGHT-RIDER/backend
dotnet restore
```
Then run:
```
dotnet run
```

The backend will be available at http://localhost:5000.

### Frontend
```
cd ../frontend
npm install
npm start
```
The app will be available at http://localhost:3000.

This project also includes an Electron shell (public/electron.js) for a desktop build. Check package.json for the exact script to launch it.

# License

See [LICENSE](./LICENSE.md). TCGRider is an independent, fan-made project and is not affiliated with, endorsed by, or sponsored by Pokémon, Topps, One Piece, or any other trading card game publisher.
