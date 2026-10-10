"""In-memory sessions. Nothing is stored on disk."""

from __future__ import annotations

import asyncio
import time
from collections import deque

from . import config
from .script import WorldState


class Session:
    def __init__(self, session_id: str):
        self.id = session_id
        self.world = WorldState()
        self.history: deque[tuple[str, str]] = deque(maxlen=6)  # (user text, assistant reply)
        self.lock = asyncio.Lock()  # one turn at a time
        self.last_active = time.monotonic()
        self._turn_times: deque[float] = deque(maxlen=config.TURNS_PER_MINUTE)

    def touch(self) -> None:
        self.last_active = time.monotonic()

    def allow_turn(self) -> bool:
        """Simple sliding-window rate limit."""
        now = time.monotonic()
        if len(self._turn_times) == self._turn_times.maxlen and now - self._turn_times[0] < 60:
            return False
        self._turn_times.append(now)
        return True

    def reset(self) -> None:
        self.world = WorldState()
        self.history.clear()


class SessionStore:
    def __init__(self):
        self._sessions: dict[str, Session] = {}

    def get(self, session_id: str) -> Session:
        self._sweep()
        session = self._sessions.get(session_id)
        if session is None:
            session = self._sessions[session_id] = Session(session_id)
        session.touch()
        return session

    async def reset(self, session_id: str) -> None:
        session = self._sessions.get(session_id)
        if session:
            async with session.lock:
                session.reset()
                session.touch()

    def _sweep(self) -> None:
        cutoff = time.monotonic() - config.SESSION_IDLE_SECONDS
        for sid in [s for s, sess in self._sessions.items() if sess.last_active < cutoff and not sess.lock.locked()]:
            del self._sessions[sid]

    def __len__(self) -> int:
        return len(self._sessions)
