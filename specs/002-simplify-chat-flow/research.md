# research.md — Simplify Chat Flow (Phase 0)

## Purpose
Resolve open questions from the feature spec and record decisions and alternatives.

---

## Decision: Single-plan comparison handling

- Decision: When a comparison request contains only one resolved plan, the system will prompt the user for the missing plan (clarification) by default.
- Rationale: Asking for the missing plan avoids making potentially incorrect assumptions and satisfies FR-005 (request clarification when input is missing). It preserves user intent and keeps responses reliable.
- Alternatives considered:
  - Auto-convert single-plan compare -> recommendation (useful when phrasing implies recommendation). Rejected as default because it may surprise users; could be implemented heuristically later.

## Decision: Multi-intent message handling & response format

- Decision: The backend will return one combined response containing labeled sections for each detected intent. Example sections: `Comparison`, `Recommendation`, `Answer`, `Clarification`.
- Rationale: Matches FR-005a and simplifies client UI handling while preserving clarity for the user.
- Implementation note: produce both a human-friendly text block and a structured JSON `ResponseArtifact` with typed sections for downstream clients and analytics.

## Decision: Public API surface

- Decision: Expose exactly one public chat endpoint (e.g., `/api/chat`). Remove or de-register specialized public endpoints for compare/suggest per FR-009 and FR-010.
- Rationale: Reduces surface area and aligns with UX goals in spec.

## Decision: Cutover strategy

- Decision: Hard cutover (per spec): remove dedicated compare/suggest endpoints and controllers at release. During development use feature-branch and test staging environment; validate migration by running integration checks against the single endpoint.
- Rationale: Specified by stakeholders; simplifies long-term maintenance.

## Decision: Conversation retention

- Decision: Retain conversation context indefinitely until explicit user reset. Persist `ConversationSession` to existing persistence chosen by service (SessionService). Ensure a lightweight retention policy can be added later.

## Retrieval and plan-resolution notes

- Use existing plan normalization logic when resolving `Insurance Plan Reference`. If plan not found, return a clear error message and/or clarification question (FR-008, FR-005).

## Implementation risks & mitigations

- Risk: Foundry orchestration behaviors (e.g., single-plan compare policy) may require configuration changes. Mitigation: add a small Foundry policy layer and feature flags to adjust behavior in staging before release.

## Next action items (Phase 1 inputs)

- Define `ResponseArtifact` JSON schema (for internal API + client).
- Update `ConversationSession` persistence and confirm fields.
- Draft API contract for `/api/chat` (input schema, output schema, error codes).
- Update frontend `chat.component` to call the single endpoint and render labeled sections.
