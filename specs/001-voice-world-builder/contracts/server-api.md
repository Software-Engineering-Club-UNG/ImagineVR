# Contract: Server API

The only network interface the headset uses. Base URL is a build setting in the app, for
example `https://imagine.example.com`.

## Common request headers

| Header | Required | Meaning |
|--------|----------|---------|
| `X-App-Token` | yes | Shared app token. Wrong or missing → `401`. |
| `X-Session-Id` | yes on `/v1/turn` and `/v1/session/reset` | Random ID the app generates at launch. |
| `X-Catalog-Version` | no | The catalog version embedded in the app. |
| `X-User-Pose` | no | On `/v1/turn`, `x,z,yaw`: world-space horizontal head position in metres and heading in degrees (+Z = 0, +X = 90). `home` means the next build starts at the origin facing +Z. |

The server accepts finite pose values with x/z within ±400 m, normalizes yaw, and rounds
position to two decimals and yaw to one. Missing, invalid, `home`, or effectively zero
poses use the world origin. The server stamps positional scene statements with `frame`
and describes remembered positions to the model relative to the supplied pose. A `clear`
resets the frame for subsequent statements because the client homes the user.

## `GET /v1/health`

Returns `200` with:

```json
{ "ok": true, "language_version": 1, "catalog_version": "2026-10-10.1", "model": "…", "stt": "local:base.en", "tts": false }
```

## `POST /v1/turn`

One user request. The response is a stream of ImagineScript statements
(see [scene-language.md](scene-language.md)).

**Request body**, one of:

- `Content-Type: audio/wav` — 16 kHz mono 16-bit PCM WAV, at most 30 seconds.
- `Content-Type: application/json` — `{ "text": "I want to see a golf course" }`.
  Used by the editor, test scripts, and typed input.

**Response**: `200`, `Content-Type: application/x-ndjson`, sent incrementally with no
buffering. Guarantees:

- For audio requests the first line is `transcript`.
- A `status` line is sent before any world statement.
- Every world statement has already been validated and had unknown IDs substituted.
- The last line is always `done`. If something fails after the stream has started, an `error`
  line precedes `done` with `outcome: "failed"`.

**Before the stream starts**, failures use status codes with a JSON body
`{ "message": "…" }`: `400` (empty or unreadable body), `401` (bad token), `413` (audio too
long), `429` (too many requests), `503` (AI provider unreachable).

**Ordering**: turns in one session are processed one at a time. A turn that arrives while
another is in progress waits and then runs.

**Timing targets**: `transcript` within 1.5 s of upload completing; first world statement
within 4 s; `done` within 20 s for a typical place.

**Headset timeouts**: 8 s with no new line → show the problem state and stop reading;
45 s total.

## `POST /v1/session/reset`

Forgets the session's world and history. Returns `204`. The app calls it at launch.
For a full controller reset the app also cancels pending work and generates a new session
ID immediately; cleanup of the old session cannot repopulate the new conversation.

## `GET /v1/audio/{id}`

Returns `audio/mpeg` for a spoken reply referenced by a `say` statement's `audio` field.
Available for at least 5 minutes after the turn. Only exists when spoken replies are on.

## Server-side session state

Kept in memory only, per `X-Session-Id`, dropped after 30 minutes idle:

- the world statements applied so far, compacted (latest `sky`, `terrain`, `fog`, `light`,
  `ambience`; live `id`s with the statement that made them);
- the last few user prompts and `say` replies.

This is what lets a follow-up such as "add more trees" modify the existing world.

## Server settings (environment variables)

| Name | Purpose |
|------|---------|
| `OPENROUTER_API_KEY` | AI access. |
| `IMAGINE_MODEL` | Model used to write ImagineScript. |
| `IMAGINE_STT` | `local` (default, Whisper on this machine) or `elevenlabs`. |
| `IMAGINE_STT_MODEL` | Whisper model size when `IMAGINE_STT=local`. Default `base.en`. |
| `ELEVENLABS_API_KEY` | Needed for `IMAGINE_STT=elevenlabs` or spoken replies. |
| `IMAGINE_TTS` | `on` or `off`. Spoken replies. |
| `IMAGINE_APP_TOKEN` | Value expected in `X-App-Token`. |
| `IMAGINE_CATALOG` | Path to `catalog.json`. Defaults to `../shared/catalog.json`. |
