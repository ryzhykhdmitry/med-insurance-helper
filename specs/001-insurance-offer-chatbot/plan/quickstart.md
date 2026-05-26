# Quickstart — Local Development (Study Project)

Prereqs
- .NET SDK
- Node.js + Angular CLI
- Azurite (Azure Blob emulator) or Azure Storage account
- Foundry credentials (set in env vars) for LLM access

⚠️ **Data Privacy Reminder**: Only ingest anonymised or publicly available insurance policy documents. Do **not** upload files containing PII (names, NIN/SSN, dates of birth, medical records, or financial account numbers). The ingestion pipeline will log a WARNING if potential PII patterns are detected — review the backend logs after each ingest run.

Steps
1. Start Azurite for local blob storage (or configure AZURE_STORAGE_CONNECTION_STRING for cloud).
2. Start backend: set environment variables (FOUNDARY_API_KEY, BLOB_CONN_STRING), then run the .NET API.
3. Start frontend: `ng serve` for Angular app.
4. Upload sample PDFs to blob storage and call `/api/ingest` to register them.
5. Trigger `/api/process/{offerId}` to extract chunks and embeddings. **Check backend logs for any PII-pattern warnings before querying.**
6. Open UI and ask queries; verify streaming responses and citations.

Env vars (examples)
- FOUNDARY_API_KEY
- BLOB_CONN_STRING
- APP_ENV=local

Notes
- For study, use Azurite locally and free Foundry test keys if available. Monitor request quotas.
