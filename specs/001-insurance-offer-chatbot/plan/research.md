# Research: Medical Insurance RAG Chatbot

Decision areas and research tasks to complete before design:

- Foundry integration (Azure Foundry): authentication, streaming API, request rate limits, cost model.
- PDF ingestion pipeline: parsing (PDF->text), page/section extraction, OCR fallback, failure handling.
- Blob storage strategy: Azurite for local development vs Azure Blob (naming, versioning, access patterns).
- Chunking & embeddings: chunk size, overlap, embedding model from Foundry, vector store choice (local vs cloud), indexing strategy.
- Citation extraction: mapping chunk → (document, page, offset) for precise citations.
- Retrieval strategy: sparse vs dense retrieval, hybrid scoring and reranking.
- Streaming responses: Foundry streaming semantics and UI integration for Angular.
- Security & secrets: local dev secrets, environment variables, foundry credentials handling.

For each item above, produce:
- Decision: recommended choice
- Rationale: short justification
- Alternatives considered

Target output: `research.md` (this file) consolidated with chosen options and links to references.