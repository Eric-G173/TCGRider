from fastapi import FastAPI
from pydantic import BaseModel
from collections import deque
from fastapi.middleware.cors import CORSMiddleware


app = FastAPI()
events = deque(maxlen=100)  # keeps last 100 events in memory

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000"],
    allow_methods=["*"],
    allow_headers=["*"],
)

class FileEvent(BaseModel):
    path: str
    event_type: str

@app.post("/analyze")
async def analyze(event: FileEvent):
    events.appendleft({
        "path": event.path,
        "event_type": event.event_type,
        "timestamp": datetime.now().isoformat()
    })
    return {"status": "ok"}

@app.get("/events")
async def get_events():
    return list(events)