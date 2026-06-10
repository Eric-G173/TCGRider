# Project Midnight Rider

AI-powered email follow-up assistant. Monitors your inbox and drafts 
follow-up messages using AI when no response is received.

## Tech Stack
- Electron — desktop app wrapper
- React — frontend UI
- Python/FastAPI — backend + Gmail API integration
- Claude API — AI draft generation

## Getting Started

### Prerequisites
- Node.js
- Python 3.x
- Gmail API credentials

### Run the app

Start the backend:
cd python-ai
uvicorn main:app --reload --port 5000

Start the frontend:
cd frontend-react
npm run dev

## Features
- Email response tracking
- Automatic 3 day follow-up notifications
- AI drafted follow-up messages
- Human approval before anything sends