# data-model.md — Simplify Chat Flow (Phase 1)

## Entities

### ConversationSession

- `id` (string, UUID): Unique session identifier.
- `userId` (string): Application user identifier (nullable for anonymous).
- `createdAt` (datetime): Session creation time.
- `updatedAt` (datetime): Last activity timestamp.
- `messages` (array of `Message`): Chronological list of exchanged messages.
- `metadata` (object): Free-form metadata (e.g., locale, device, experimental flags).

### Message

- `id` (string, UUID)
- `role` (enum: `user` | `assistant` | `system`)
- `text` (string)
- `timestamp` (datetime)
- `inferredIntents` (array of `Intent`): Detected intents with confidence scores.

### Intent

- `name` (string): e.g., `compare`, `recommend`, `ask`.
- `confidence` (float 0..1)
- `parameters` (object): Named parameters (e.g., plan identifiers).

### InsurancePlanReference

- `id` (string): Normalized plan identifier used internally.
- `displayName` (string)
- `source` (string): Data source (catalog, ingestion, external).

### ResponseArtifact

- `sessionId` (string)
- `sections` (array): Each section has `type` (e.g., `comparison`, `recommendation`, `answer`, `clarification`), `content` (human text), and `payload` (structured data, optional).
- `errors` (array): User-visible error messages or diagnostics.

## Validation rules

- Messages must include at least one `role` and `text`.
- `InsurancePlanReference` lookups must resolve to a normalized `id` or trigger a clarification flow.

## Mapping to code

- Existing `ConversationSession`, `Message`, and `InsuranceOffer` models in `src/backend/MedInsuranceHelper.Api/Models` should be reviewed and extended to match above fields where necessary.
