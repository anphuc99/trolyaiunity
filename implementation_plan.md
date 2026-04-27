# Kế hoạch chuyển đổi Server Logic sang Client

## Mục tiêu
Loại bỏ hoàn toàn Node.js/Express server và chuyển tất cả logic xử lý lên Unity client, cho phép ứng dụng hoạt động độc lập mà không cần duy trì server.

## Tổng quan kiến trúc hiện tại

### Server (Node.js + Express)
```
server/
├── src/
│   ├── controllers/     (15 controller modules)
│   ├── models/          (19 TypeORM entities)
│   ├── services/        (18 service files)
│   ├── routes/          (17 route files)
│   ├── prompts/         (2 system prompt files)
│   ├── middleware/      (auth middleware)
│   └── data-source.ts   (TypeORM config: MySQL/SQLite)
```

### Client (Unity C#)
```
Assets/
├── Core/Infrastructure/Network/  (HttpClient → gọi server API)
├── Features/GamePlay/SubFeatures/
│   ├── Chat/      (chat UI + gọi server)
│   ├── Journal/   (journal UI + gọi server)
│   ├── Practice/  (translation practice)
│   ├── PracticeVocabulary/ (vocabulary review)
│   ├── Story/     (story management)
│   ├── MyLog/     (diary feature)
│   └── ...
```

---

## Phân tích Server Logic cần di chuyển

### Nhóm 1: Database (TypeORM → SQLite local trên client)

19 database entities cần chuyển sang SQLite trên client:

| Entity | Mô tả | Độ phức tạp |
|--------|--------|-------------|
| `UserEntity` | Thông tin user (username, password hash, level) | Thấp - single user |
| `LevelEntity` | CEFR/HSK levels (seeded data) | Thấp - static data |
| `CharacterEntity` | AI characters (name, personality, voice) | Trung bình |
| `CharacterRelationshipEntity` | Quan hệ giữa characters | Cao |
| `StoryEntity` | Story metadata | Trung bình |
| `JournalEntity` | Chat journal entries | Trung bình |
| `MessageEntity` | Journal messages | Trung bình |
| `JournalReviewEntity` | Journal review scheduling | Trung bình |
| `TranslationCardEntity` | Translation practice cards | Trung bình |
| `TranslationReviewEntity` | FSRS review state | Trung bình |
| `VocabularyEntity` | Collected vocabulary | Trung bình |
| `VocabularyMemoryEntity` | Vocabulary memory links | Trung bình |
| `VocabularyReviewEntity` | Vocabulary review state | Trung bình |
| `StreakEntity` | Daily streak tracking | Thấp |
| `MyLogEntity` | Diary entries | Trung bình |
| `MyLogJournalEntity` | Diary journals | Trung bình |
| `MyLogMessageEntity` | Diary messages | Trung bình |
| `VoiceEntity` | TTS voice configs | Thấp - static |
| `LearningPathEntity` | Learning paths | Thấp |

---

### Nhóm 2: AI Services (gọi trực tiếp từ client)

| Service | Mô tả | Giải pháp client-side |
|---------|--------|----------------------|
| `gemini.service.ts` | Gemini chat API | Gọi Gemini REST API trực tiếp từ Unity (UnityWebRequest) |
| `openai.service.ts` | OpenAI chat API | Gọi OpenAI REST API trực tiếp từ Unity |
| `chat-prompt.service.ts` | Build system prompt (444 dòng) | Chuyển sang C# class `ChatPromptBuilder` |
| `chat-history.service.ts` | Lưu/load chat history (file-based) | SQLite local hoặc file JSON local |
| `cheap-ai.service.ts` | Gemini Flash Lite (memory/intent) | Gọi Gemini API trực tiếp |

> [!IMPORTANT]
> **Bảo mật API Key**: Khi gọi API trực tiếp từ client, API keys sẽ bị lộ trong app binary. Đây là trade-off chấp nhận được khi không có server, nhưng cần lưu ý:
> - API keys nên được lưu trong `PlayerPrefs` (encrypted) hoặc file config riêng
> - User cần tự cung cấp API key của mình (không hardcode)
> - Cân nhắc dùng Google API key restrictions (chỉ cho phép từ package name)

---

### Nhóm 3: TTS (Text-to-Speech) + Audio Processing

| Service | Mô tả | Giải pháp client-side |
|---------|--------|----------------------|
| `tts.service.ts` | OpenAI TTS + cache audio | Gọi OpenAI TTS API trực tiếp, cache trên local storage |
| `gemini-tts.service.ts` | Gemini TTS + fallback pipeline | Gọi Gemini TTS API trực tiếp |

> [!WARNING]
> **FFmpeg rất quan trọng**: Server hiện dùng FFmpeg để:
> 1. **Pitch shift** (`asetrate` + `aresample`) — tạo giọng thanh hơn, trẻ con hơn cho characters. Đây là yếu tố quan trọng cho trải nghiệm nghe.
> 2. **Trim silence** (`silenceremove`) — loại bỏ khoảng lặng đầu/cuối audio
> 3. **WAV → MP3** (`libmp3lame`) — convert format
>
> Logic pitch trên server:
> ```
> detuneCents = pitch * 50
> detuneFactor = 2^(detuneCents / 1200)
> effectiveRate = speakingRate * detuneFactor
> ffmpeg -af "asetrate=<sampleRate * effectiveRate>,aresample=<sampleRate>"
> ```

#### 4 giải pháp thay thế FFmpeg trên client (chọn 1, quyết định sau):

**Giải pháp A: Ship FFmpeg binary + gọi qua `System.Diagnostics.Process`**
- Đơn giản nhất, copy-paste logic từ server
- Kết quả **100% giống server**
- Đặt `ffmpeg.exe` vào `StreamingAssets/ffmpeg/`
- Gọi qua `Process.Start()` giống server dùng `execFile()`
- ⚠️ **Chỉ chạy trên Windows/Mac/Linux**, không chạy trên Android/iOS
- ⚠️ App nặng thêm ~80MB

**Giải pháp B: FFmpeg Kit (native library cho mọi platform)**
- Dùng [FFmpeg Kit](https://github.com/arthenica/ffmpeg-kit) — pre-built native libraries
- Đặt `.dll/.so/.a` vào `Assets/Plugins/` cho từng platform
- Gọi qua P/Invoke: `[DllImport("ffmpegkit")] static extern int execute(string command);`
- ✅ Chạy trên **tất cả platforms** (Windows, Android, iOS)
- ✅ Kết quả **100% giống server**
- ⚠️ App nặng thêm ~15-30MB

**Giải pháp C: FFmpeg for Unity (Asset Store plugin, ~$50)**
- [FFmpeg for Unity](https://assetstore.unity.com/packages/tools/video/ffmpeg-for-unity-132865)
- Đã build sẵn cho Windows, macOS, Linux, Android, iOS
- API C# clean, setup nhanh nhất
- ✅ Chạy trên **tất cả platforms**
- ✅ Kết quả **100% giống server**
- ⚠️ Tốn phí ~$50, app nặng thêm ~15-30MB

**Giải pháp D: Pure C# Sinc Resampling (không cần FFmpeg)**
- Implement thuật toán windowed sinc interpolation trong C# (giống `aresample` của FFmpeg)
- Xử lý offline trên raw PCM data trước khi tạo `AudioClip`
- Trim silence bằng scan sample amplitudes
- Không cần convert MP3 — Unity play WAV/PCM trực tiếp
- ✅ Chạy trên **tất cả platforms**, không thêm size
- ⚠️ Chất lượng **~95% giống server** (phụ thuộc vào filter taps và window function)
- ⚠️ Cần test kỹ để đảm bảo nghe tương đương

| | Platform | Size thêm | Chất lượng | Công sức |
|---|---|---|---|---|
| **A. Ship binary** | Win/Mac/Linux | +80MB | 100% | Thấp |
| **B. FFmpeg Kit** | Tất cả ✅ | +15-30MB | 100% | Trung bình |
| **C. Asset Store** | Tất cả ✅ | +15-30MB | 100% | Thấp nhất |
| **D. C# Sinc** | Tất cả ✅ | +0MB | ~95% | Trung bình |

---

### Nhóm 4: Spaced Repetition (FSRS)

| Service | Mô tả | Giải pháp client-side |
|---------|--------|----------------------|
| `fsrs.service.ts` | ts-fsrs scheduling | Port sang C# hoặc dùng C# FSRS library |

> [!NOTE]
> Thuật toán FSRS có thể port sang C# dễ dàng. Đã có [open-source C# implementations](https://github.com/open-spaced-repetition/fsrs4net).

---

### Nhóm 5: Long-term Memory (ChromaDB)

| Service | Mô tả | Giải pháp |
|---------|--------|-----------|
| `vector-memory.service.ts` | ChromaDB vector store | **Loại bỏ** hoặc dùng local embedding |
| `memory-extraction.service.ts` | Extract memory từ AI reply | Giữ logic, lưu vào SQLite |
| `memory-retrieval.service.ts` | Retrieve + compress pipeline | Simplify: keyword search trong SQLite |
| `cheap-ai.service.ts` | Intent rewrite + compression | Gọi Gemini API trực tiếp |

> [!IMPORTANT]
> **Quyết định cần user review**: ChromaDB là vector database cần server riêng. Có 2 lựa chọn:
> 
> **A) Loại bỏ hoàn toàn long-term memory** — Đơn giản nhất, mất tính năng nhớ dài hạn
> 
> **B) Thay thế bằng SQLite FTS5** — Dùng full-text search thay vector search, hiệu quả kém hơn nhưng vẫn có memory. Memory text được lưu + search bằng keyword matching thay vì embedding similarity.
>
> **C) Local embedding** — Dùng một model nhỏ (vd: ONNX Runtime + mini embedding model) để tính embedding trên client, search bằng cosine similarity trong SQLite. Phức tạp nhất nhưng gần nhất với server.

---

### Nhóm 6: Authentication

| Server Logic | Giải pháp client-side |
|---|---|
| JWT token + bcrypt password | **Loại bỏ hoàn toàn** — single user, không cần auth |
| Registration/Login flow | **Đơn giản hóa** — user profile lưu local, không cần đăng nhập |
| Token refresh/validate | **Loại bỏ** |

---

### Nhóm 7: Relationship Evaluation

| Service | Mô tả | Giải pháp |
|---------|--------|-----------|
| `relationship-update.service.ts` | AI đánh giá quan hệ sau session | Gọi Gemini API trực tiếp từ client |
| Character relationships CRUD | DB operations | SQLite local |

---

## Open Questions

> [!IMPORTANT]
> ### 1. Long-term Memory
> Bạn muốn xử lý long-term memory như thế nào?
> - **A)** Loại bỏ hoàn toàn (đơn giản nhất)
> - **B)** SQLite FTS5 keyword search (trung bình)
> - **C)** Local embedding + cosine similarity (phức tạp nhất)

> [!IMPORTANT]
> ### 2. API Key Management
> User sẽ tự cung cấp API key (Google/OpenAI) trong app settings, đúng không? Hay bạn có kế hoạch khác?

> [!IMPORTANT]
> ### 3. Avatar Upload
> Server hiện lưu avatar vào `public/avatars/`. Khi chuyển sang client-only:
> - Lưu avatar vào `Application.persistentDataPath`
> - Không còn URL HTTP, sẽ dùng local file path

> [!IMPORTANT]
> ### 4. Transcribe (Speech-to-Text)
> Server hiện dùng OpenAI `gpt-4o-mini-transcribe` cho speech-to-text. Trên client:
> - Gọi OpenAI Whisper API trực tiếp (cần OpenAI API key)
> - Hoặc dùng Google Speech-to-Text API
> - Hoặc dùng on-device STT (Unity Whisper plugin)

> [!IMPORTANT]
> ### 5. Phạm vi chuyển đổi
> Bạn muốn chuyển tất cả cùng lúc, hay chuyển từng phần? Tôi đề xuất thứ tự:
> 1. Database layer (SQLite) + loại bỏ auth
> 2. AI Chat (Gemini/OpenAI trực tiếp) + Chat History local  
> 3. TTS trực tiếp
> 4. Journal / Translation / Vocabulary (FSRS)
> 5. MyLog (Diary)
> 6. Long-term Memory (nếu giữ)
> 7. Relationship evaluation

---

## Proposed Changes

### Phase 1: Client-side Database Layer

#### [NEW] `Assets/Core/Infrastructure/Database/LocalDatabase.cs`
- Wrapper cho SQLite trên Unity (sử dụng [SQLite4Unity3d](https://github.com/robertohuertasm/SQLite4Unity3d) hoặc `SQLite-net`)
- Singleton pattern, khởi tạo khi app start
- Auto-migration / table creation
- Path: `Application.persistentDataPath/mimi_chat.db`

#### [NEW] `Assets/Core/Infrastructure/Database/Entities/` (1 file per entity)
- Port 19 TypeORM entities sang C# POCO classes
- Mapping TypeORM decorators → SQLite-net attributes (`[Table]`, `[PrimaryKey]`, `[AutoIncrement]`, etc.)

#### [NEW] `Assets/Core/Infrastructure/Database/Repositories/`
- Repository pattern cho mỗi entity
- CRUD operations sử dụng SQLite-net

#### [NEW] `Assets/Core/Infrastructure/Database/SeedData.cs`
- Seed levels (HSK1-HSK6) và voices khi lần đầu chạy app

---

### Phase 2: AI Service Layer (gọi trực tiếp)

#### [NEW] `Assets/Core/Infrastructure/AI/GeminiDirectService.cs`
- Gọi Gemini REST API trực tiếp qua `UnityWebRequest`
- Endpoint: `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
- Hỗ trợ: chat history, system instruction, audio parts
- Session cache tương tự server

#### [NEW] `Assets/Core/Infrastructure/AI/OpenAIDirectService.cs`
- Gọi OpenAI API trực tiếp qua `UnityWebRequest`
- Endpoint: `https://api.openai.com/v1/responses`
- Hỗ trợ: system instruction, history

#### [NEW] `Assets/Core/Infrastructure/AI/ChatPromptBuilder.cs`
- Port `chat-prompt.service.ts` (444 dòng) sang C#
- Build system prompt với level config, context, story, relationships
- Giữ nguyên JSON response format specification

#### [NEW] `Assets/Core/Infrastructure/AI/ChatHistoryLocal.cs`
- Lưu/load chat history vào file JSON local
- Interface tương tự `ChatHistoryStore` trên server
- Path: `Application.persistentDataPath/chat-history/`

#### [MODIFY] `Assets/Core/Infrastructure/Network/HttpClient.cs`
- Giữ nguyên `HttpClient` class cho backward compatibility
- Thêm static flag `UseDirectAPIs` để bypass server
- Khi `UseDirectAPIs = true`, các request sẽ được redirect sang local services

---

### Phase 3: TTS Layer

#### [NEW] `Assets/Core/Infrastructure/AI/TTSDirectService.cs`
- Gọi OpenAI TTS API trực tiếp: `https://api.openai.com/v1/audio/speech`
- Request format: `{ model, input, voice, response_format: "mp3" }`
- Cache audio file vào `Application.persistentDataPath/audio/`
- Audio ID hashing tương tự server

#### [NEW] `Assets/Core/Infrastructure/AI/GeminiTTSDirectService.cs`
- Gọi Gemini TTS API trực tiếp
- Model: `gemini-3.1-flash-tts-preview`
- Round-robin key rotation (nếu có nhiều keys)
- Fallback: Hanzi → Pinyin → IPA khi TTS fail

---

### Phase 4: FSRS (Spaced Repetition)

#### [NEW] `Assets/Core/Infrastructure/FSRS/FSRSScheduler.cs`
- Port `ts-fsrs` logic sang C#
- Hoặc integrate thư viện [fsrs4net](https://github.com/open-spaced-repetition/fsrs4net)
- Interface: `UpdateReviewAfterRating(state, rating) → newState`

---

### Phase 5: Feature-by-Feature Controller Migration

Mapping chi tiết: Server Controller/Service → Client Feature Controller.
Mỗi server endpoint được chuyển thành local logic trong đúng controller của Feature tương ứng.

---

#### 5.1 Feature: `StartScene` → Loại bỏ Auth

**File**: `Assets/Features/StartScene/Scripts/Controller/StartSceneController.cs`
**Server**: `users.controller.ts`, `auth.service.ts`

| Server endpoint (loại bỏ) | Thay bằng |
|---|---|
| `POST /api/token/validate` | Check local user profile exists |
| `POST /api/token/refresh` | Loại bỏ |
| `GET /api/characters` | SQLite: `LocalDatabase.Query<CharacterEntity>()` |

- Bỏ toàn bộ JWT validate/refresh flow
- Nếu có user profile local → vào GamePlay, nếu không → vào Login (first-time setup)

---

#### 5.2 Feature: `Login` → First-time Setup

**File**: `Assets/Features/Login/Scripts/Controller/LoginController.cs`
**Server**: `users.controller.ts`

| Server endpoint (loại bỏ) | Thay bằng |
|---|---|
| `POST /api/users/login` | Tạo user profile local (SQLite) |
| `GET /api/characters` | SQLite query |

- Đổi flow: không đăng nhập, chỉ setup profile lần đầu (tên, level, API keys)

---

#### 5.3 SubFeature: `GamePlay/Chat` → AI Chat trực tiếp

**File**: `Assets/Features/GamePlay/SubFeatures/Chat/Scripts/Controller/ChatController.cs` (51KB)
**Server**: `chat.controller.ts` (58KB), `chat-prompt.service.ts`, `gemini.service.ts`, `chat-history.service.ts`, `memory-retrieval.service.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `POST /api/chat/send` | `GeminiDirectService.CreateReply()` hoặc `OpenAIDirectService.CreateReply()` |
| `GET /api/chat/history` | `ChatHistoryLocal.Load()` |
| `POST /api/chat/developer` | `ChatHistoryLocal.AppendDeveloperMessage()` |
| `GET /api/chat/developer-state` | Parse local history file |
| `POST /api/chat/edit` | Edit local history + re-run AI |
| `POST /api/chat/transcribe` | Gọi OpenAI Whisper API trực tiếp |
| `GET /api/text-to-speech` | `TTSDirectService.GenerateAudio()` + FFmpeg pitch |

Thêm vào controller:
- `ChatPromptBuilder.BuildSystemPrompt()` — port từ `chat-prompt.service.ts`
- Memory extraction từ AI reply — port từ `memory-extraction.service.ts`
- Memory recall pipeline (nếu giữ) — port từ `memory-retrieval.service.ts`

---

#### 5.4 SubFeature: `GamePlay/Journal` → Local DB queries

**File**: `Assets/Features/GamePlay/SubFeatures/Journal/Scripts/Controller/JournalController.cs` (25KB)
**Server**: `journal.controller.ts` (36KB)

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/journals` | SQLite: query `JournalEntity` by storyId |
| `GET /api/journals/:id` | SQLite: query `JournalEntity` + `MessageEntity` |
| `GET /api/journals/search` | SQLite: LIKE query trên messages |
| `POST /api/journals/end` | AI summarize (Gemini trực tiếp) + save journal/messages vào SQLite + clear chat history local |
| `GET /api/journals/review/due` | SQLite: query `JournalReviewEntity` where nextReviewDate <= now |
| `POST /api/journals/review` | FSRS update + SQLite save |

---

#### 5.5 Feature: `JournalOverlay` → Local DB + TTS

**File**: `Assets/Features/JournalOverlay/Scripts/Controller/JournalOverlayController.cs`
**Server**: `journal.controller.ts`, `tts.service.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/journals/:id` | SQLite query |
| `GET /api/text-to-speech` | `TTSDirectService.GenerateAudio()` |

---

#### 5.6 SubFeature: `GamePlay/Practice` → Translation + FSRS local

**File**: `Assets/Features/GamePlay/SubFeatures/Practice/Scripts/Controller/PracticeController.cs` (15KB)
**Server**: `translation.controller.ts` (29KB), `fsrs.service.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/translation/due` | SQLite: join `TranslationCardEntity` + `TranslationReviewEntity` where due |
| `GET /api/translation/learn` | SQLite: query messages without cards |
| `GET /api/translation` | SQLite: query all cards with reviews |
| `GET /api/translation/context/:messageId` | SQLite: query 5 messages before/after |
| `POST /api/translation/review` | `FSRSScheduler.UpdateReviewAfterRating()` + SQLite save |
| `POST /api/translation/explain` | Gọi Gemini AI trực tiếp cho explanation |
| `GET /api/text-to-speech` | `TTSDirectService.GenerateAudio()` |

---

#### 5.7 SubFeature: `GamePlay/PracticeVocabulary` → Vocab + FSRS local

**File**: `Assets/Features/GamePlay/SubFeatures/PracticeVocabulary/Scripts/Controller/PracticeVocabularyController.cs` (13KB)
**Server**: `vocabulary.controller.ts` (41KB), `fsrs.service.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/vocabulary/due` | SQLite: join `VocabularyEntity` + `VocabularyReviewEntity` where due |
| `POST /api/vocabulary/:id/review` | `FSRSScheduler.UpdateReviewAfterRating()` + SQLite save |
| `PUT /api/vocabulary/:id/ignore` | SQLite: delete or flag |
| `GET /api/text-to-speech` | `TTSDirectService.GenerateAudio()` |

---

#### 5.8 SubFeature: `GamePlay/Story` → SQLite CRUD

**File**: `Assets/Features/GamePlay/SubFeatures/Story/Scripts/Controller/StoryController.cs` (7KB)
**Server**: `story.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/stories` | SQLite: query `StoryEntity` |
| `POST /api/stories` | SQLite: insert `StoryEntity` |
| `PUT /api/stories/:id` | SQLite: update `StoryEntity` |
| `DELETE /api/stories/:id` | SQLite: delete `StoryEntity` |

---

#### 5.9 SubFeature: `GamePlay/MyLog` → Diary AI + SQLite

**File**: `Assets/Features/GamePlay/SubFeatures/MyLog/Scripts/Controller/MyLogController.cs` (9KB)
**Server**: `mylog.controller.ts` (73KB), `mylog-prompt.service.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/mylog` | SQLite: query `MyLogEntity` |
| `POST /api/mylog` | SQLite: insert `MyLogEntity` |
| `PUT /api/mylog/:id` | SQLite: update |
| `DELETE /api/mylog/:id` | SQLite: delete |
| `POST /api/mylog/chat/send` | Gọi Gemini AI trực tiếp + local chat history |
| `GET /api/mylog/chat/history` | Local file/SQLite |
| `POST /api/mylog/chat/end` | AI summarize + save messages vào SQLite |
| `POST /api/mylog/chat/developer` | Local chat history append |

Thêm: port `mylog-prompt.service.ts` → `MyLogPromptBuilder.cs` trong MyLog/Scripts/Infrastructure/

---

#### 5.10 SubFeature: `GamePlay/Character` → SQLite CRUD

**File**: `Assets/Features/GamePlay/SubFeatures/Character/Scripts/Controller/CharacterController.cs` (4KB)
**Server**: `characters.controller.ts`, `character-relationships.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/characters` | SQLite: query `CharacterEntity` |
| `GET /api/character-relationships` | SQLite: query `CharacterRelationshipEntity` |
| `POST /api/character-relationships/evaluate-session` | `CheapAIService` (Gemini) + SQLite update |

---

#### 5.11 Features: `CreateCharater` + `EditCharacter` + `CharacterInfo` → SQLite + Local Avatar

**Files**:
- `Assets/Features/CreateCharater/Scripts/Controller/CreateCharaterController.cs`
- `Assets/Features/EditCharacter/Scripts/Controller/EditCharacterController.cs`
- `Assets/Features/CharacterInfo/Scripts/Controller/CharacterInfoController.cs`

**Server**: `characters.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `POST /api/characters` | SQLite: insert `CharacterEntity` |
| `PUT /api/characters/:id` | SQLite: update `CharacterEntity` |
| `DELETE /api/characters/:id` | SQLite: delete |
| `POST /api/characters/upload-avatar` | Save base64 → file local (`persistentDataPath/avatars/`) |
| `GET /api/voices` | SQLite: query `VoiceEntity` (seeded data) |

---

#### 5.12 SubFeature: `GamePlay/Home` → Local queries

**File**: `Assets/Features/GamePlay/SubFeatures/Home/Scripts/Controller/HomeController.cs` (3KB)
**Server**: `home.controller.ts`, `streak.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/streak` | SQLite: query `StreakEntity` |
| Dashboard data | SQLite aggregate queries |

---

#### 5.13 SubFeature: `GamePlay/Task` → Local task calculation

**File**: `Assets/Features/GamePlay/SubFeatures/Task/Scripts/Controller/TaskController.cs` (3KB)
**Server**: `tasks.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/tasks/today` | Tính toán locally: count due translations, due vocab, streak |

---

#### 5.14 SubFeature: `GamePlay/Setting` → Local profile + API keys

**File**: `Assets/Features/GamePlay/SubFeatures/Setting/Scripts/Controller/SettingController.cs` (9KB)
**Server**: `users.controller.ts`, `levels.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/users/me` | SQLite: query local user profile |
| `PUT /api/users/profile` | SQLite: update user profile |
| `GET /api/levels` | SQLite: query `LevelEntity` (seeded) |
| `GET /api/stories` | SQLite: query `StoryEntity` |
| `PUT /api/users/current-story` | SQLite: update user currentStoryId |

Thêm: UI nhập/thay đổi API keys (Google, OpenAI)

---

#### 5.15 SubFeature: `GamePlay/LearningPath` → SQLite

**File**: `Assets/Features/GamePlay/SubFeatures/LearningPath/Scripts/Controller/LearningPathController.cs` (8KB)
**Server**: `learning-path.controller.ts`

| Server endpoint | Thay bằng logic local |
|---|---|
| `GET /api/learning-paths` | SQLite: query `LearningPathEntity` |
| CRUD operations | SQLite CRUD |

---

#### 5.16 Parent: `GamePlay` Controller → Orchestration

**File**: `Assets/Features/GamePlay/Scripts/Controller/GamePlayController.cs` (27KB)
**Server**: `chat.controller.ts` (relationship wiring, session management)

- Giữ nguyên logic orchestration subfeatures
- Thay HTTP calls (nếu có) → local services
- Relationship evaluation post-session → `CheapAIService` trực tiếp

---

### Phase 6: Loại bỏ Auth & Simplify Login

#### [MODIFY] `Assets/Core/Infrastructure/Authentication/AuthTokenModel.cs`
- Loại bỏ JWT logic
- Single user mode: auto-"login" với user profile local

#### [MODIFY] `Assets/Features/Login/Scripts/Controller/LoginController.cs`
- Đơn giản hóa: skip login nếu user profile exists
- First-time setup: chọn level, nhập tên, cung cấp API keys

#### [MODIFY] `Assets/Features/StartScene/Scripts/Controller/StartSceneController.cs`
- Bỏ token validate/refresh
- Check local profile → route to Login or GamePlay

---

### Phase 7: Settings & API Key Management

#### [NEW] `Assets/Core/Infrastructure/Settings/AppSettings.cs`
- Lưu API keys (Google, OpenAI) vào encrypted PlayerPrefs
- Lưu TTS preferences, model selection
- UI cho settings page để user nhập/thay đổi keys

---

## Verification Plan

### Automated Tests
- Unit test cho `FSRSScheduler` (port từ server tests)
- Unit test cho `ChatPromptBuilder` (verify output giống server)
- Unit test cho `LocalDatabase` CRUD operations

### Manual Verification
1. **Chat flow**: Gửi message → nhận AI response → hiển thị đúng format
2. **TTS**: Generate audio → play → nghe đúng
3. **Journal**: End chat → summary → save → load lại đúng
4. **Translation practice**: Review → FSRS scheduling đúng
5. **Vocabulary**: Collect → review → schedule đúng
6. **Offline mode**: App hoạt động khi không có internet (trừ AI calls)
7. **Data persistence**: Tắt app → mở lại → data còn nguyên

---

## Ước lượng công việc

| Phase | Ước lượng | Rủi ro |
|-------|-----------|--------|
| Phase 1: Database | 3-4 ngày | Thấp |
| Phase 2: AI Services | 4-5 ngày | Trung bình (API compatibility) |
| Phase 3: TTS | 2-3 ngày | Trung bình (audio processing) |
| Phase 4: FSRS | 1-2 ngày | Thấp |
| Phase 5: Business Logic | 5-7 ngày | Cao (nhiều file cần sửa) |
| Phase 6: Auth removal | 1 ngày | Thấp |
| Phase 7: Settings | 1-2 ngày | Thấp |
| **Tổng** | **~17-24 ngày** | |

> [!CAUTION]
> ### Rủi ro chính
> 1. **API Key exposure**: API keys nằm trên client, có thể bị decompile
> 2. **API rate limits**: Không có server proxy, client gọi trực tiếp → mỗi user chịu rate limit riêng
> 3. **Data loss**: Không có cloud backup, data chỉ nằm trên device
> 4. **Audio processing**: Không có FFmpeg trên mobile, cần giải pháp thay thế
> 5. **CORS/Platform restrictions**: Một số API có thể block request từ non-browser clients (thường không ảnh hưởng Unity)
