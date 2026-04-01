# Server API Documentation

Base URL: `http://localhost:4000`

All API endpoints are served under prefix: `/api`.

## Conventions

### Authentication

This API uses a dual-token authentication system:

- **Access Token**: Short-lived (1 hour), used for API requests.
- **Refresh Token**: Long-lived (7 days), used to obtain new access tokens.

**Usage:**

1. Login returns both `accessToken` and `refreshToken`.
2. Use access token in API requests: `Authorization: Bearer <accessToken>`
3. When access token expires, use `/api/token/refresh` with refresh token to get a new access token.
4. Use `/api/token/validate` with refresh token to validate and extend its expiry.

**Error responses:**

- `401 { "message": "Unauthorized" }` – missing token
- `401 { "code": "TOKEN_EXPIRED", "message": "Access token has expired" }` – access token expired (use refresh token)
- `401 { "message": "Invalid token", "error": "..." }` – invalid token

### JSON

- Requests: `Content-Type: application/json` (unless otherwise noted)
- Responses: JSON (unless otherwise noted)

### Dates

- `Date` fields are serialized by `JSON.stringify(Date)` (typically ISO 8601 strings).
- Some “today” calculations use timezone `Asia/Ho_Chi_Minh`.

### Common error shape

Many handlers return:

```json
{ "message": "...", "error": "..." }
```

on HTTP `500`.

---

## Shared

### GET /api/health

Health check.

- Auth: no
- Response `200`:

```json
{ "status": "ok" }
```

### POST /api/token/validate

Validate a refresh token and extend its expiry by 7 days.

- Auth: no
- Body:

```json
{ "refreshToken": "<refresh_token>" }
```

- Response `200`:

```json
{ "valid": true, "refreshToken": "<new_refresh_token>" }
```

- Errors:
  - `400 { "valid": false, "message": "Refresh token is required" }`
  - `401 { "valid": false, "code": "TOKEN_EXPIRED", "message": "Refresh token has expired" }`
  - `401 { "valid": false, "message": "Invalid refresh token", "error": "..." }`

### POST /api/token/refresh

Use a refresh token to get a new access token.

- Auth: no
- Body:

```json
{ "refreshToken": "<refresh_token>" }
```

- Response `200`:

```json
{ "accessToken": "<new_access_token>" }
```

- Errors:
  - `400 { "message": "Refresh token is required" }`
  - `401 { "code": "TOKEN_EXPIRED", "message": "Refresh token has expired" }`
  - `401 { "message": "Invalid refresh token", "error": "..." }`

### GET /api/text-to-speech

Generate (or fetch cached) TTS audio.

- Auth: no
- Query params:
  - `text` (string, required) – text to speak
  - `tone` (string, optional, default: `"neutral, medium pitch"`)
  - `voice` (string, optional) – OpenAI voice name override
  - `force` (string, optional) – when `"true"`, delete cached audio and regenerate
- Response `200`:

```json
{ "success": true, "output": "<audioId>", "url": "/audio/<audioId>.mp3" }
```

- Error `400`:

```json
{ "message": "Text is required" }
```

---

## Users

### POST /api/users/register

Create a new user (requires `REGISTRATION_TOKEN`).

- Auth: no
- Body:

```json
{ "username": "string", "password": "string", "registerToken": "string" }
```

Notes:
- `username` is normalized to lowercase and trimmed.
- Password must be at least 6 characters.

- Response `201`:

```json
{
  "user": {
    "id": 1,
    "username": "example",
    "levelId": null,
    "level": null,
    "levelDescription": null
  },
  "accessToken": "<access_token>",
  "refreshToken": "<refresh_token>"
}
```

Notes:
- `accessToken` expires in 1 hour.
- `refreshToken` expires in 7 days.

- Errors:
  - `400 {"message":"Username and password are required"}`
  - `403 {"message":"Invalid registration token"}`
  - `409 {"message":"Username is already taken"}`
  - `500 {"message":"Registration token is not configured"}`

### POST /api/users/login

Login with username + password.

- Auth: no
- Body:

```json
{ "username": "string", "password": "string" }
```

- Response `200`:

```json
{
  "user": { "id": 1, "username": "example", "levelId": 1, "level": "A1", "levelDescription": "...", "currentStoryId": null },
  "accessToken": "<access_token>",
  "refreshToken": "<refresh_token>"
}
```

Notes:
- `accessToken` expires in 1 hour.
- `refreshToken` expires in 7 days.

- Errors:
  - `400 {"message":"Username and password are required"}`
  - `401 {"message":"Invalid credentials"}`

### POST /api/users/reset-password

Reset password using `REGISTRATION_TOKEN` (no old password required).

- Auth: no
- Body:

```json
{ "username": "string", "newPassword": "string", "registerToken": "string" }
```

- Response `200`:

```json
{ "message": "Password updated" }
```

- Errors:
  - `400 {"message":"Username and new password are required"}`
  - `403 {"message":"Invalid registration token"}`
  - `404 {"message":"User not found"}`

### GET /api/users/me

Get current user profile.

- Auth: yes
- Response `200`:

```json
{ "user": { "id": 1, "username": "example", "levelId": 1, "level": "A1", "levelDescription": "...", "currentStoryId": 1 } }
```

- Errors:
  - `404 {"message":"User not found"}`

### PUT /api/users/level

Update user level.

- Auth: yes
- Body:

```json
{ "levelId": 1 }
```

- Response `200` (returns new token pair):

```json
{
  "user": { "id": 1, "username": "example", "levelId": 1, "level": "A1", "levelDescription": "...", "currentStoryId": null },
  "accessToken": "<access_token>",
  "refreshToken": "<refresh_token>"
}
```

- Errors:
  - `400 {"message":"Level is required"}`
  - `404 {"message":"Level not found"}`
  - `404 {"message":"User not found"}`

### PUT /api/users/current-story

Set the user's current story. Used as fallback when APIs don't receive a storyId parameter.

- Auth: yes
- Body:

```json
{ "storyId": 1 }
```

Pass `null` to clear the current story:

```json
{ "storyId": null }
```

- Response `200`:

```json
{ "user": { "id": 1, "username": "example", "levelId": 1, "level": "A1", "levelDescription": "...", "currentStoryId": 1 } }
```

- Errors:
  - `400 {"message":"Story id is required"}`
  - `404 {"message":"Story not found"}` – story must belong to the authenticated user
  - `404 {"message":"User not found"}`

---

## Levels

### GET /api/levels

List CEFR levels.

- Auth: no
- Response `200`:

```json
{
  "levels": [
    {
      "id": 1,
      "level": "A0",
      "maxWords": 5,
      "guideline": "...",
      "descript": "...",
      "createdAt": "2026-02-23T...Z",
      "updatedAt": "2026-02-23T...Z"
    }
  ]
}
```

---

## Characters

All endpoints in this group require auth.

### POST /api/characters/upload-avatar

Upload an avatar as a base64 image data URL.

- Auth: yes
- Body:

```json
{ "image": "data:image/png;base64,...", "filename": "optional.png" }
```

- Response `200`:

```json
{ "url": "http://localhost:4000/public/avatars/<file>.png" }
```

Notes:
- Only `png/jpeg/jpg/webp` are accepted.
- Max size: 2MB.

### GET /api/characters

List characters for the current user.

- Auth: yes
- Response `200` (array, not wrapped):

```json
[
  {
    "id": 1,
    "name": "Mimi",
    "personality": "...",
    "gender": "female",
    "appearance": null,
    "avatar": "https://..." ,
    "voiceModel": "openai",
    "voiceName": "alloy",
    "pitch": 0,
    "speakingRate": 1,
    "createdAt": "2026-02-23T...Z",
    "updatedAt": "2026-02-23T...Z"
  }
]
```

### POST /api/characters

Create a character.

- Auth: yes
- Body:

```json
{
  "name": "string",
  "personality": "string",
  "gender": "male|female",
  "appearance": "string|null",
  "avatar": "string|null",
  "voiceModel": "openai|null",
  "voiceName": "string|null",
  "pitch": 0,
  "speakingRate": 1
}
```

- Response `201`: CharacterResponse (same shape as GET item)

### PUT /api/characters/:id

Update a character.

- Auth: yes
- Path params:
  - `id` (number)
- Body: same as create (required fields must be present)
- Response `200`: CharacterResponse

### DELETE /api/characters/:id

Delete a character.

- Auth: yes
- Response `204` (empty body)

---

## Chat

All endpoints in this group require auth.

### POST /api/chat/send

Send a chat message to the selected model (OpenAI or Gemini), and append to server-side history.

- Auth: yes
- Body:

```json
{
  "message": "string",
  "sessionId": "string (optional)",
  "model": "string (optional)",

  "context": "string (optional)",
  "storyPlot": "string (optional)",
  "relationshipSummary": "string (optional)",
  "contextSummary": "string (optional)",
  "relatedStoryMessages": "string (optional)",
  "checkPronunciation": false,
  "storyId": 123
}
```

Notes:
- `model` decides the provider. Gemini is used when the model name matches `gemini-*`.
- The server builds a system prompt using the authenticated user’s level and optional fields above.

- Response `200`:

```json
{ "reply": "<assistant reply string>", "model": "<effective model>" }
```

- Errors:
  - `400 {"message":"Message is required"}`
  - `500 {"message":"OpenAI API key is not configured"}` or `Gemini API key is not configured`

### POST /api/chat/transcribe

Transcribe recorded voice to plain text for chat input using OpenAI `gpt-4o-mini-transcribe`.

- Auth: yes
- Body:

```json
{
  "audio": "data:audio/wav;base64,<...>",
  "language": "vi"
}
```

Notes:
- `audio` is required and must be a base64 audio data URL.
- Maximum payload size is 12 MB.
- `language` is optional (for example: `vi`, `ko`, `en`).

- Response `200`:

```json
{ "transcript": "..." }
```

- Errors:
  - `400 {"message":"audio is required"}`
  - `400 {"message":"Invalid audio data URL"}`
  - `413 {"message":"Audio payload is too large"}`
  - `500 {"message":"Failed to transcribe audio"}`

### GET /api/chat/history

Get the current chat history (excluding `system` and `developer` messages).

- Auth: yes
- Query params:
  - `sessionId` (string, optional)
- Response `200`:

```json
{
  "messages": [
    { "role": "user", "content": "..." },
    { "role": "assistant", "content": "..." }
  ]
}
```

### GET /api/chat/developer-state

Returns which characters are currently “active” based on developer messages in history.

- Auth: yes
- Query params:
  - `sessionId` (string, optional)
- Response `200`:

```json
{ "activeCharacterNames": ["Mimi", "..." ] }
```

### POST /api/chat/developer

Append a developer message to history. Used for:
- `character_added`
- `character_removed`
- `context_update`

- Auth: yes
- Body:

```json
{
  "sessionId": "string (optional)",
  "kind": "character_added|character_removed|context_update",
  "character": {
    "name": "string",
    "age": "number (optional)",
    "personality": "string (optional)",
    "gender": "string (optional)",
    "appearance": "string (optional)"
  },
  "context": "string (required when kind=context_update)"
}
```

- Response `200`:

```json
{ "ok": true }
```

### POST /api/chat/edit

Edit a previous message.

There are two modes:

1) Edit assistant content (adds a developer note; does not re-run the model)
- Body:

```json
{
  "kind": "assistant",
  "assistantMessageId": "string",
  "content": "string",
  "sessionId": "string (optional)"
}
```

- Response `200`: `{ "ok": true }`

2) Edit a user message by index (rebuilds history up to that user message, re-runs the model, and replaces subsequent turns)
- Body:

```json
{
  "kind": "user",
  "userMessageIndex": 0,
  "content": "string",
  "sessionId": "string (optional)",
  "model": "string (optional)",

  "context": "string (optional)",
  "storyPlot": "string (optional)",
  "relationshipSummary": "string (optional)",
  "contextSummary": "string (optional)",
  "relatedStoryMessages": "string (optional)",
  "checkPronunciation": false,
  "storyId": 123
}
```

- Response `200`:

```json
{
  "messages": [ {"role":"user","content":"..."}, {"role":"assistant","content":"..."} ],
  "reply": "<assistant reply string>",
  "model": "<effective model>"
}
```

---

## Journals

All endpoints in this group require auth.

### GET /api/journals

List journals (optionally filtered by story).

- Auth: yes
- Query params:
  - `storyId` (number, optional) – falls back to user's `currentStoryId` if not provided
- Response `200`:

```json
{
  "journals": [
    { "id": 1, "summary": "...", "createdAt": "2026-02-23T...Z" }
  ]
}
```

### GET /api/journals/:id

Get a journal and its messages.

- Auth: yes
- Path params:
  - `id` (number)
- Response `200`:

```json
{
  "journal": { "id": 1, "summary": "...", "createdAt": "2026-02-23T...Z" },
  "messages": [
    {
      "id": "<uuid>",
      "content": "...",
      "characterName": "User|Mimi|...",
      "translation": "...",
      "tone": "...",
      "audio": "<audioId>",
      "createdAt": "2026-02-23T...Z"
    }
  ]
}
```

### GET /api/journals/search

Search messages across all journals.

- Auth: yes
- Query params:
  - `q` (string, required) – search text or regex pattern
  - `limit` (number, optional, default 50, max 100)
- Response `200`:

```json
{
  "results": [
    {
      "messageId": "<uuid>",
      "journalId": 1,
      "journalDate": "2026-02-23T...Z",
      "content": "...",
      "characterName": "...",
      "translation": "...",
      "tone": "...",
      "audio": "..."
    }
  ],
  "total": 1,
  "hasMore": false
}
```

### POST /api/journals/end

Finalize the current conversation:
- asks OpenAI to summarize in Vietnamese,
- stores a journal entry,
- stores messages into DB,
- clears the file-backed chat history.

- Auth: yes
- Body:

```json
{ "sessionId": "string (optional)", "storyId": 123 }
```

Notes:
- If `storyId` is not provided, the user's `currentStoryId` is used as fallback.

- Response `200`:

```json
{ "journalId": 1, "summary": "..." }
```

- Errors:
  - `400 {"message":"No conversation history to summarize"}`
  - `500 {"message":"OpenAI API key is not configured"}`

---

## Stories

All endpoints in this group require auth.

### GET /api/stories

- Response `200`:

```json
{ "stories": [ { "id": 1, "name": "...", "description": "...", "currentProgress": null, "createdAt": "...", "updatedAt": "..." } ] }
```

### GET /api/stories/:id

- Response `200`: `{ "story": <StoryResponse> }`

### POST /api/stories

- Body:

```json
{ "name": "string", "description": "string", "currentProgress": "string|null" }
```

- Response `201`: `{ "story": <StoryResponse> }`

### PUT /api/stories/:id

- Body (at least one field must be present):

```json
{ "name": "string?", "description": "string?", "currentProgress": "string|null?" }
```

- Response `200`: `{ "story": <StoryResponse> }`

### DELETE /api/stories/:id

- Response `200`:

```json
{ "ok": true }
```

---

## Streak

All endpoints in this group require auth.

### GET /api/streak

- Response `200`:

```json
{ "currentStreak": 0, "longestStreak": 0, "lastCompletedDate": null }
```

---

## Tasks

All endpoints in this group require auth.

### GET /api/tasks/today

Get daily tasks progress (timezone `Asia/Ho_Chi_Minh`).

- Response `200`:

```json
{
  "date": "2026-02-23",
  "tasks": [
    {
      "id": "translation_new",
      "label": "Luyện tập 10 câu mới",
      "type": "count",
      "progress": 3,
      "target": 10,
      "remaining": 7,
      "completed": false
    },
    {
      "id": "translation_due",
      "label": "Ôn tập hết câu đến hạn",
      "type": "clear_due",
      "progress": 0,
      "target": 0,
      "remaining": 2,
      "completed": false
    }
  ],
  "completedCount": 0,
  "totalCount": 2
}
```

---

## Translation (Luyện tập)

All endpoints in this group require auth.

### GET /api/translation

List translation cards with review + journal summary.

- Response `200`:

```json
{
  "cards": [
    {
      "id": 1,
      "messageId": "<uuid>",
      "content": "...",
      "translation": "...",
      "userTranslation": null,
      "characterName": "Mimi",
      "tone": "neutral, medium pitch",
      "explanationMd": "...",
      "journalId": 1,
      "userId": 1,
      "createdAt": "...",
      "updatedAt": "...",
      "journalSummary": "...",
      "review": {
        "id": 1,
        "translationCardId": 1,
        "stability": 0,
        "difficulty": 5,
        "lapses": 0,
        "currentIntervalDays": 1,
        "nextReviewDate": "...",
        "lastReviewDate": null,
        "isStarred": false,
        "reviewHistory": []
      }
    }
  ]
}
```

### GET /api/translation/due

Get due cards (sorted by the original message `createdAt` ascending).

- Response `200`:

```json
{ "cards": [/* same card shape as above */], "total": 1 }
```

### GET /api/translation/stats

- Response `200`:

```json
{
  "totalCards": 10,
  "withReview": 8,
  "withoutReview": 2,
  "dueToday": 3,
  "starredCount": 1,
  "difficultCount": 2
}
```

### GET /api/translation/learn

Return candidates for “learn new sentences”.

- Response `200`:

```json
{
  "candidates": [
    {
      "messageId": "<uuid>",
      "content": "...",
      "translation": "...",
      "characterName": "...",
      "tone": "neutral, medium pitch|null",
      "journalId": 1,
      "journalSummary": "...",
      "createdAt": "..."
    }
  ]
}
```

Notes:
- Candidates are picked from the most recent 1000 messages.
- Only messages with non-empty `translation` are returned.

### GET /api/translation/context/:messageId

Return context hints around a message (5 before + 5 after) in the same journal.

- Path params:
  - `messageId` (string)
- Response `200`:

```json
{
  "before": [
    { "messageId": "<uuid>", "characterName": "User|...", "text": "..." }
  ],
  "after": [
    { "messageId": "<uuid>", "characterName": "User|...", "text": "..." }
  ]
}
```

Text rules:
- If `characterName` is `"User"` (case-insensitive) → `text` is the original `content`.
- Otherwise → `text` is Vietnamese `translation`.

### POST /api/translation/explain

Generate (or return cached) AI explanation Markdown for a card.

- Body:

```json
{ "cardId": 1, "messageId": "<uuid>", "userTranslation": "string" }
```

Rules:
- Either `cardId` or `messageId` is required.
- If `messageId` is provided and the card does not exist, the server creates it.

- Response `200`:

```json
{ "explanation": "<markdown>", "card": { /* card fields */ } }
```

### POST /api/translation/review

Submit a FSRS rating.

- Body:

```json
{ "rating": 1, "cardId": 1, "messageId": "<uuid>", "userTranslation": "string" }
```

Rules:
- `rating` must be 1–4.
- If the card does not exist, `messageId` is required and a new card is created from the message.

- Response `200`:

```json
{ "card": { /* card fields */ }, "review": { /* review fields */ } }
```

### PUT /api/translation/:id/star

Toggle star for a translation card.

- Path params:
  - `id` (number) – translation card id
- Response `200`: Review JSON (same as `review` shape)

### POST /api/translation/transcribe

Transcribe user audio (Korean) using OpenAI.

- Body:

```json
{ "audio": "data:audio/webm;base64,..." }
```

- Response `200`:

```json
{ "transcript": "..." }
```

- Errors:
  - `400 {"message":"audio is required"}`
  - `400 {"message":"Invalid audio data URL"}`
  - `413 {"message":"Audio payload is too large"}` (max 12MB)

---

## Vocabulary

All endpoints in this group require auth.

### GET /api/vocabulary

List vocabularies with review + memory.

- Response `200`:

```json
{
  "vocabularies": [
    {
      "id": "<uuid>",
      "korean": "...",
      "vietnamese": "...",
      "isManuallyAdded": true,
      "userId": 1,
      "createdAt": "...",
      "updatedAt": "...",
      "review": { /* review fields */ },
      "memory": { /* memory fields */ }
    }
  ]
}
```

### GET /api/vocabulary/stats

- Response `200`:

```json
{
  "totalVocabularies": 10,
  "withReview": 10,
  "withoutReview": 0,
  "dueToday": 3,
  "starredCount": 1,
  "difficultCount": 2
}
```

### GET /api/vocabulary/learned-count

Count words that were actually reviewed at least once.

- Response `200`:

```json
{
  "count": 7
}
```

### GET /api/vocabulary/due

Get vocabularies due today.

- Response `200`:

```json
{ "vocabularies": [/* same item shape */], "total": 1 }
```

### GET /api/vocabulary/:id

Get a vocabulary with review + memory.

- Path params:
  - `id` (string) – vocabulary uuid
- Response `200` (not wrapped):

```json
{
  "id": "<uuid>",
  "korean": "...",
  "vietnamese": "...",
  "isManuallyAdded": true,
  "userId": 1,
  "createdAt": "...",
  "updatedAt": "...",
  "review": { /* review fields */ },
  "memory": { /* memory fields */ }
}
```

### POST /api/vocabulary

Collect a new vocabulary.

- Body:

```json
{
  "korean": "string",
  "vietnamese": "string",
  "memory": "string (optional)",
  "linkedMessageIds": ["<messageId>"],
  "difficultyRating": "very_easy|easy|medium|hard"
}
```

- Response `201`: created vocabulary object with `review` and `memory`.

### PUT /api/vocabulary/:id

Update vocabulary text.

- Body:

```json
{ "korean": "string?", "vietnamese": "string?" }
```

- Response `200`: updated vocabulary entity

### DELETE /api/vocabulary/:id

- Response `200`:

```json
{ "message": "Vocabulary deleted" }
```

### POST /api/vocabulary/:id/review

Submit FSRS rating for the vocab’s review.

- Body:

```json
{ "rating": 1 }
```

- Response `200`: Review JSON

### PUT /api/vocabulary/:id/memory

Create/update memory for a vocabulary.

- Body:

```json
{ "userMemory": "string", "linkedMessageIds": ["<messageId>"] }
```

- Response `200`:

```json
{
  "id": 1,
  "vocabularyId": "<uuid>",
  "userMemory": "...",
  "linkedMessageIds": ["<uuid>"],
  "createdAt": "...",
  "updatedAt": "..."
}
```

### PUT /api/vocabulary/:id/star

Toggle starred state.

- Response `200`: Review JSON

### PUT /api/vocabulary/:id/direction

Set card direction.

- Body:

```json
{ "direction": "kr-vn" }
```

- Response `200`: Review JSON

---

## Static assets (non-/api)

These are not under `/api` but are served by the same server:

- `GET /public/...` – static assets
  - character avatars typically live under `/public/avatars/...`
- `GET /audio/<audioId>.mp3` – cached TTS audio files
