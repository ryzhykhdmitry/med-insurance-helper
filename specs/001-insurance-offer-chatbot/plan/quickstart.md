# Quickstart — Local Development (Study Project)

Prereqs
- .NET SDK
- Node.js + Angular CLI
- Azurite (Azure Blob emulator) or Azure Storage account
- Foundry credentials (set in env vars) for LLM access

Steps
1. Start Azurite for local blob storage (or configure AZURE_STORAGE_CONNECTION_STRING for cloud).
2. Start backend: set environment variables (FOUNDARY_API_KEY, BLOB_CONN_STRING), then run the .NET API.
3. Start frontend: `ng serve` for Angular app.
4. Upload sample PDFs to blob storage and call `/api/ingest` to register them.
5. Trigger `/api/process/{offerId}` to extract chunks and embeddings.
6. Open UI and ask queries; verify streaming responses and citations.

Env vars (examples)
- FOUNDARY_API_KEY
- BLOB_CONN_STRING
- APP_ENV=local

Notes
- For study, use Azurite locally and free Foundry test keys if available. Monitor request quotas.
