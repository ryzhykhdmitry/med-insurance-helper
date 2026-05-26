# Feature Specification: Medical Insurance RAG Chatbot

**Feature Branch**: `001-insurance-offer-chatbot`

**Created**: 2026-05-21

**Status**: Draft

**Input**: User description: "I'm building modern RAG chatbot helper. It should help user to find information about offers of medical insurance companies (stored in PDF), be able to compare offers and choose best suitable according to the user needs. PDFs will be stored in the blob storage. The system will have chat like UI for the user with streaming and citations."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Ask About a Specific Insurance Offer (Priority: P1)

A user opens the chat interface and asks a question in natural language about a specific medical insurance product — for example, coverage limits, exclusions, or premium amounts. The system searches the stored PDF documents, retrieves the relevant passages, and streams a response back to the user with citations referencing the source document and page.

**Why this priority**: This is the core value proposition — answering factual questions from real insurance documents. Without this, nothing else is useful.

**Independent Test**: Can be fully tested by uploading a single PDF, asking one factual question, and verifying the streamed answer references the correct passage from the document.

**Acceptance Scenarios**:

1. **Given** a PDF insurance offer is stored in the system, **When** the user asks "What is the annual coverage limit for hospitalisation?", **Then** the system streams a relevant answer and cites the source document name and section.
2. **Given** the system has no matching information, **When** the user asks a question outside the scope of all stored PDFs, **Then** the system responds clearly that no relevant information was found, without fabricating an answer.
3. **Given** the user submits a question, **When** the response is being generated, **Then** the answer appears progressively (streamed) rather than appearing all at once after a delay.

---

### User Story 2 — Compare Two or More Insurance Offers (Priority: P2)

A user asks the chatbot to compare specific aspects (e.g., dental coverage, deductibles, premium cost) across two or more insurance companies. The system retrieves relevant passages from each insurer's PDF and presents a side-by-side or structured comparison in the chat, with citations for each data point.

**Why this priority**: Comparison is the second most critical user need — helping users make an informed choice is the primary goal of the tool.

**Independent Test**: Can be tested by uploading PDFs from two insurers and asking "Compare dental coverage between Company A and Company B", verifying both sources are cited.

**Acceptance Scenarios**:

1. **Given** PDFs from at least two insurers are stored, **When** the user asks to compare a specific benefit across them, **Then** the system returns a structured comparison with citations from each respective document.
2. **Given** one insurer's PDF does not mention the requested benefit, **When** comparison is requested, **Then** the system notes the missing information clearly for that insurer rather than omitting it silently.

---

### User Story 3 — Get a Best-Match Recommendation (Priority: P3)

A user describes their personal situation and preferences (e.g., "I need cover for a family of four with good dental and vision") and asks the chatbot for a recommendation. The system evaluates the available insurance offers against the stated criteria and suggests the most suitable option(s), with reasoning and citations.

**Why this priority**: Personalised recommendation is the highest-value outcome, but depends on P1 and P2 being solid first.

**Independent Test**: Can be tested independently by uploading multiple PDFs and asking for a recommendation based on a stated requirement, verifying the response includes a named recommendation with supporting citations.

**Acceptance Scenarios**:

1. **Given** multiple insurer PDFs are available, **When** the user provides personal criteria, **Then** the system recommends one or more offers with an explanation of why each fits, citing relevant document sections.
2. **Given** no stored offer matches the user's criteria well, **When** a recommendation is requested, **Then** the system acknowledges the limitation and presents the closest available match with caveats.

---

### User Story 4 — Continue a Multi-Turn Conversation (Priority: P4)

A user asks follow-up questions within the same chat session, referencing earlier responses. The system maintains conversational context so the user does not need to repeat themselves.

**Why this priority**: Conversational continuity significantly improves usability but is not required for the first working version.

**Independent Test**: Can be tested by asking a question, then asking a follow-up that references the previous answer (e.g., "What about that plan's vision coverage?"), and verifying the system understands the reference.

**Acceptance Scenarios**:

1. **Given** a prior exchange in the session, **When** the user asks a follow-up with an implicit reference, **Then** the system correctly resolves the reference from conversation history and responds accurately.

---

### Edge Cases

- What happens when a PDF is corrupted or unreadable during document ingestion?
- How does the system handle questions containing ambiguous insurer names (e.g., two companies with similar names)?
- What happens when the user's question is very broad and matches large portions of many documents?
- How does the system behave when all PDFs are removed or unavailable?
- What if the streamed response is interrupted mid-way by a network error?

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to type natural language questions into a chat interface and receive streamed responses.
- **FR-002**: Every response that draws on document content MUST include citations identifying the source document and the relevant section or page.
- **FR-003**: The system MUST retrieve relevant passages from stored PDF insurance documents to ground its answers.
- **FR-004**: Users MUST be able to request a comparison of a specific topic across two or more insurance offers in a single query.
- **FR-005**: Users MUST be able to describe their needs and receive a recommendation of the most suitable offer from those available.
- **FR-006**: The system MUST maintain conversation context within a session so users can ask follow-up questions without repeating prior context.
- **FR-007**: The system MUST clearly indicate when no relevant information is found, rather than generating an unsupported answer.
- **FR-008**: PDF documents MUST be retrievable from blob storage for processing and search.
- **FR-009**: The chat interface MUST display responses as a progressive stream, not as a single block delivered after a delay.
- **FR-010**: The system MUST handle ingestion failures (e.g., unreadable PDFs) gracefully and notify the operator without crashing.

### Key Entities

- **Insurance Offer (PDF Document)**: A file from a medical insurance company containing policy details, coverage terms, exclusions, and pricing. Identified by insurer name and document version.
- **Document Chunk**: A passage or section extracted from an Insurance Offer PDF, used as the unit of retrieval and citation.
- **Conversation Session**: A single continuous chat interaction for one user, containing an ordered history of questions and answers.
- **Message**: A single user question or system response within a Conversation Session.
- **Citation**: A reference attached to a response indicating the source Insurance Offer document and location of the supporting passage.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can ask a factual question and receive a streamed, cited answer within 10 seconds of submission for typical queries.
- **SC-002**: At least 90% of answers to questions that have a clear answer in the stored documents correctly cite the relevant source.
- **SC-003**: Users can complete a comparison of two insurance offers in a single conversation turn without needing to reformulate their query.
- **SC-004**: Users can receive a personalised recommendation based on stated criteria in under 3 conversation turns.
- **SC-005**: The system correctly reports "no information found" rather than hallucinating an answer in at least 95% of out-of-scope queries tested.
- **SC-006**: Follow-up questions within the same session are correctly interpreted in context at least 85% of the time.

---

## Data Privacy & Sensitive Content Policy *(mandatory)*

> ⚠️ **Operator Warning**: Only ingest publicly available or fully anonymised insurance policy documents. Do **not** upload files that contain personally identifiable information (PII) such as names, NIN/SSN, dates of birth, medical records, or financial account details. Redact or exclude any such content before ingestion.

- The ingestion pipeline MUST log a warning when heuristic checks flag potential sensitive content (e.g., patterns resembling NIN, date-of-birth, credit card, or IBAN formats in extracted text). No automatic blocking is required for this study project, but the operator must be notified so they can review.
- Sample PDFs added to `docs/samples/` MUST be fully anonymised dummy data — real personal or patient data MUST NOT be committed to the repository.
- This policy aligns with the project constitution: "Avoid ingesting sensitive personal data. Redact or exclude sensitive information before ingestion."

---

## Assumptions

- Users interact via a web-based chat interface; mobile support is out of scope for v1.
- PDF documents are pre-loaded into blob storage by an operator; end-user PDF upload is out of scope for v1.
- No user authentication is required for the study project; a single shared session model is acceptable.
- The number of PDF documents is small (tens, not thousands) — suitable for a study/local environment.
- All PDFs are in English; multi-language support is out of scope.
- The system runs locally or on a free cloud tier; high-availability and SLA are not required.
- Conversation history is in-memory per session; persistence across sessions is out of scope for v1.
