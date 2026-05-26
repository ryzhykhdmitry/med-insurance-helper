# API Contract — Simplify Chat Flow

## POST /api/chat

Request (application/json)

```json
{
  "sessionId": "string | null",
  "userId": "string | null",
  "message": {
    "text": "string"
  },
  "contextHints": { }
}
```

Response (application/json)

```json
{
  "sessionId": "string",
  "responseArtifact": {
    "sections": [
      { "type": "comparison", "content": "...", "payload": { } },
      { "type": "recommendation", "content": "...", "payload": { } }
    ],
    "errors": []
  }
}
```

Status codes:
- `200 OK` — Request handled; `responseArtifact` populated.
- `400 Bad Request` — Invalid input (missing user message etc.).
- `404 Not Found` — Referenced plans not found (use clarification flow instead).
- `500 Internal Server Error` — Unexpected server error.

Notes:
- The endpoint replaces specialized compare/suggest endpoints; clients should migrate to this single endpoint.
- The `responseArtifact.sections` ordering is from highest-priority to lowest and should be rendered by clients in that order.
