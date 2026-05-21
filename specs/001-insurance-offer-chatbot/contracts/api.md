# API Contracts (HTTP) — Medical Insurance RAG Chatbot

Base: /api

Endpoints

- POST /api/ingest
  - Description: Notify backend of a new PDF in blob storage (or upload).
  - Body: { "blobUri": string, "insurerName": string, "title": string }
  - Response: 202 Accepted { "offerId": string }

- POST /api/process/{offerId}
  - Description: Trigger parsing, chunking, embedding for an InsuranceOffer.
  - Response: 200 OK { "status": "processing" }

- POST /api/search
  - Description: Retrieve relevant passages for a query.
  - Body: { "query": string, "topK": int }
  - Response: 200 OK { "results": [ { "offerId": string, "snippet": string, "page": int, "score": number } ] }

- POST /api/compare
  - Description: Compare multiple offers on specified aspects.
  - Body: { "offerIds": [string], "aspects": [string] }
  - Response: 200 OK { "comparison": { "aspect": { "offerId": "snippet" } } }

- POST /api/recommend
  - Description: Recommend best offers for given user criteria.
  - Body: { "criteria": string }
  - Response: 200 OK { "recommendations": [ { "offerId": string, "reason": string, "citations": [ {"offerId":string, "page":int, "excerpt":string} ] } ] }

- POST /api/session
  - Description: Create or continue conversation session.
  - Body: { "sessionId": string (optional) }
  - Response: 200 OK { "sessionId": string }

Streaming
- Use server-sent events or websocket for streaming assistant responses and progressive citations.
