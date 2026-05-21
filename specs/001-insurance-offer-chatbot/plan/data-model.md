# Data Model: Medical Insurance RAG Chatbot

Entities

- InsuranceOffer
  - id: string (UUID)
  - insurer_name: string
  - title: string
  - blob_uri: string (blob storage path)
  - uploaded_at: datetime
  - version: string
  - status: enum (uploaded, processed, failed)

- DocumentChunk
  - id: string (UUID)
  - offer_id: FK -> InsuranceOffer.id
  - text: string
  - start_page: int
  - end_page: int
  - offset: int
  - length: int
  - embedding: vector (stored externally or referenced)
  - created_at: datetime

- ConversationSession
  - id: string (UUID)
  - started_at: datetime
  - last_active_at: datetime
  - metadata: json (user prefs)

- Message
  - id: string (UUID)
  - session_id: FK -> ConversationSession.id
  - role: enum (user, assistant, system)
  - text: string
  - created_at: datetime
  - citations: array of Citation

- Citation
  - document_id: InsuranceOffer.id
  - chunk_id: DocumentChunk.id
  - page_ref: string
  - excerpt: string

Validation rules
- Chunk text must not exceed configured max length.
- Each DocumentChunk must reference a valid InsuranceOffer.

Assumptions
- Embeddings may be stored in a vector DB or locally as files for study project.
- Scale is small (tens of PDFs); embed storage can be local file-backed vector store.
