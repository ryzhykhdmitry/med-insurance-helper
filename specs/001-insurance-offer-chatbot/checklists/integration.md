# Integration Demo Checklist

**Feature**: Medical Insurance RAG Chatbot
**Purpose**: Manual validation of the end-to-end flow after local setup

## Prerequisites

- [ ] Azurite running (`scripts/setup-azurite.ps1 -Command start`)
- [ ] Backend API running (`dotnet run` in `src/backend/MedInsuranceHelper.Api`)
- [ ] Frontend running (`npm start` in `src/frontend`)
- [ ] Environment variables set (copy `.env.example` → configure `FOUNDRY_API_KEY` and `FOUNDRY_ENDPOINT`)
- [ ] Sample documents loaded (`scripts/load-samples.ps1`)

---

## Step 1: Document Ingestion

- [ ] `POST /api/ingest` returns `202 Accepted` with a valid `offerId`
- [ ] `POST /api/process/{offerId}` returns `200 OK` with `{"status": "processing"}`
- [ ] Backend logs show: parsing pages → chunking → embedding → vector store saved
- [ ] Vector store file created at `data/vectors/{offerId}.json`

---

## Step 2: Search (US1)

- [ ] `POST /api/search` with `{"query": "annual coverage limit", "topK": 3}` returns results
- [ ] Results include `offerId`, `snippet`, `page`, and `score` fields
- [ ] Scores are between 0 and 1

---

## Step 3: Streaming Chat with Citations (US1)

- [ ] Open browser at `http://localhost:4200/chat`
- [ ] Ask: *"What is the annual coverage limit for hospitalisation?"*
- [ ] Response streams in token-by-token (visible in UI)
- [ ] Citations are displayed below the response (document ID + page reference)
- [ ] No hallucinated information (answer references only ingested documents)

---

## Step 4: No-Results Guard (US1)

- [ ] Ask a question with no relevant coverage (e.g. car insurance coverage)
- [ ] System returns a "no relevant information found" message
- [ ] Foundry is NOT called when no chunks are retrieved (check logs)

---

## Step 5: Compare Offers (US2)

- [ ] Navigate to `http://localhost:4200/compare`
- [ ] Enter two offer IDs and aspects (e.g. "dental coverage, vision")
- [ ] `POST /api/compare` returns a comparison table
- [ ] Missing data is shown with "No data" badge (not an error)
- [ ] Aspect cells contain relevant snippets from each offer

---

## Step 6: Recommendations (US3)

- [ ] Navigate to `http://localhost:4200/recommend`
- [ ] Enter criteria: *"Family of four with dental and vision coverage, budget €250/month"*
- [ ] `POST /api/recommend` returns ranked recommendations
- [ ] Each recommendation includes a reason and source citations
- [ ] Fallback message shown when no perfect match found

---

## Step 7: Multi-Turn Conversation (US4)

- [ ] Navigate to `http://localhost:4200/chat`
- [ ] Ask: *"What is Alpha Health's annual limit?"* → receive answer
- [ ] Ask follow-up: *"What about the dental coverage for that plan?"*
- [ ] System resolves "that plan" from session history
- [ ] Session ID is consistent across requests (check browser dev tools / network tab)
- [ ] `POST /api/session` creates a session and returns `sessionId`

---

## Step 8: Error Handling

- [ ] Call `POST /api/process/{invalid-id}` → returns `404 Not Found`
- [ ] Stop Azurite and call `POST /api/ingest` → error is logged but API returns meaningful error
- [ ] Backend logs show structured JSON error entries (not stack traces in production)

---

## Step 9: Observability

- [ ] Backend logs include `[INF]` level entries for all major operations
- [ ] PII detection warnings appear in logs when PII-like patterns are found in PDFs
- [ ] Ingestion failure notification logged when pipeline fails (check `INGESTION_FAILURE` log entry)
- [ ] Request logs show HTTP method, path, and duration (Serilog request logging)

---

## Completion

- [ ] All 9 steps passed → **System is integration-ready**
- [ ] Any failed steps → document issues in `specs/001-insurance-offer-chatbot/checklists/issues.md`
