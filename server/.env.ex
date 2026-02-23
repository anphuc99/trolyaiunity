# Example environment variables for development
# Copy this file to .env and adjust values as needed.

NODE_ENV=development

# Database
# Choose which DB driver TypeORM should use.
# Supported: mysql | sqlite
DB_TYPE=mysql

# MySQL settings (used when DB_TYPE=mysql)
DB_HOST=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=
DB_NAME=mimi_chat

# SQLite settings (used when DB_TYPE=sqlite)
# Absolute or repo-root relative path.
# Note: SQLite mode uses TypeORM's `sqljs` driver (WASM) to avoid native builds on Windows.
DB_SQLITE_PATH=server/data/sqlite/mimi_chat.sqlite

# Server
PORT=4000

# OpenAI
OPENAI_API_KEY=
OPENAI_MODEL=gpt-4o-mini
# OpenAI TLS / corporate proxy (optional)
# Provide a custom CA bundle if your network injects a self-signed certificate.
# OPENAI_TLS_CA_CERT_PATH=C:\\path\\to\\corp-root-ca.pem
# OPENAI_TLS_CA_CERT_BASE64=
# Dev mode default: TLS verification is skipped.
# Set false to force strict TLS in dev:
# OPENAI_TLS_INSECURE=false
# Optional TTS settings
# OPENAI_TTS_MODEL=gpt-4o-mini-tts-2025-03-20
# OPENAI_TTS_VOICE=alloy
# Optional override for the system prompt file
# OPENAI_SYSTEM_PROMPT_PATH=src/prompts/chat.system.txt

# Auth
JWT_SECRET=change-me
REGISTRATION_TOKEN=change-me

# Chat history persistence (optional)
# Defaults to server/data/chat-history
# CHAT_HISTORY_DIR=

# TypeORM / ORM options (optional)
# TYPEORM_SYNCHRONIZE=false
# TYPEORM_LOGGING=false

# Example MySQL connection (for reference):
# mysql://root:password@localhost:3306/mimi_chat

# Example SQLite connection (for reference):
# DB_TYPE=sqlite
# DB_SQLITE_PATH=server/data/sqlite/mimi_chat.sqlite
