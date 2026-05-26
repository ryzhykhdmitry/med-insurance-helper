# Feature Specification: Simplify Chat Flow

**Feature Branch**: `002-simplify-chat-flow`

**Created**: 2026-05-26

**Status**: Draft

**Input**: User description: "The solution is overwhelmed. The user should be able to communicate through the chat via natural language to ask for comparison or something else. No need to have compare and suggest functions separately. Remove all unused functions, controllers etc"

## Clarifications

### Session 2026-05-26

- Q: What cutover strategy should be used for removing separate compare/suggest endpoints and controllers? → A: Hard cutover with immediate removal and no compatibility layer.
- Q: How should the system handle messages containing multiple supported intents? → A: Handle all detected supported intents in one response with clearly separated sections.
- Q: How long should conversation context be retained? → A: Retain context indefinitely until user explicitly resets it.
- Q: What public API surface should remain after unification? → A: Expose only one public chat endpoint and remove compare/suggest public endpoints.
- Q: How should the system handle comparison requests containing only one plan name? → A: Behavior to be decided in Foundry.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask Anything in One Chat (Priority: P1)

A user can type natural language requests in one chat interface to compare plans, request recommendations, or ask follow-up questions without choosing a separate tool first.

**Why this priority**: This is the core user value and directly addresses the current overload caused by multiple separate feature entry points.

**Independent Test**: Can be fully tested by submitting different request types in one chat session and confirming the system returns the correct response type each time.

**Acceptance Scenarios**:

1. **Given** a user is in the main chat, **When** the user asks to compare two or more plans, **Then** the system returns a comparison response in the same chat thread.
2. **Given** a user is in the main chat, **When** the user asks for a recommendation, **Then** the system returns a recommendation response in the same chat thread.
3. **Given** a user is in the main chat, **When** the user asks a general insurance question, **Then** the system answers in context without redirecting to a separate workflow.

---

### User Story 2 - Continue Context Across Requests (Priority: P2)

A user can ask follow-up questions after any response and the system keeps context so the user does not need to restate all details.

**Why this priority**: Multi-turn continuity is required for natural conversation and significantly reduces friction.

**Independent Test**: Can be tested by asking a comparison question followed by a recommendation and verifying follow-up responses include prior context.

**Acceptance Scenarios**:

1. **Given** a user asked for a comparison, **When** the user follows with "which one is better for family coverage?", **Then** the response uses earlier compared plans as context.
2. **Given** the user asks an ambiguous follow-up, **When** the system cannot infer missing details, **Then** it asks a concise clarification question before answering.

---

### User Story 3 - Streamlined Interface and Behavior (Priority: P3)

A user no longer sees separate compare or suggest modes and can complete the same outcomes from one conversation surface.

**Why this priority**: Simplifying entry points lowers cognitive load and improves discoverability.

**Independent Test**: Can be tested by validating the UI and API behavior no longer expose separate specialized chat workflows to end users.

**Acceptance Scenarios**:

1. **Given** a user opens the application, **When** they view available chat actions, **Then** they see a single chat experience instead of separate compare and suggest experiences.
2. **Given** an existing user flow that previously relied on separate actions, **When** the user submits equivalent natural language in the main chat, **Then** the user still receives an equivalent outcome.

### Edge Cases

- What happens when the user asks for a comparison but provides only one plan name? This behavior is determined by the configured Foundry orchestration policy.
- How does the system structure a single response when one message combines multiple intents (for example, comparison plus recommendation)?
- What happens when the user requests an explicit context reset and then asks a follow-up question that depends on prior conversation state?
- How does the system respond when requested plans are not found in available data?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide one primary chat interaction path for supported insurance-assistant tasks.
- **FR-002**: System MUST accept natural language requests for comparison, recommendation, and general plan questions in the same chat experience.
- **FR-003**: System MUST determine one or more supported intents from message content and return corresponding results within the same conversation.
- **FR-004**: System MUST preserve conversation context across turns indefinitely until the user explicitly resets conversation context.
- **FR-005**: System MUST request clarification when user input is missing required details to produce a reliable answer.
- **FR-005a**: System MUST return a single combined response with clearly separated sections when multiple supported intents are detected in one user message.
- **FR-006**: System MUST maintain equivalent user outcomes currently available through separate compare and suggest flows.
- **FR-007**: System MUST hard-remove redundant dedicated compare/suggest interaction paths with no compatibility adapter once unified chat is released.
- **FR-008**: System MUST return user-friendly error messages when requests cannot be fulfilled due to missing data, unsupported requests, or internal failures.
- **FR-009**: System MUST expose exactly one public chat endpoint for user-request handling in this feature scope.
- **FR-010**: System MUST ensure deprecated specialized paths no longer appear in user navigation, user-facing API usage guidance, user documentation, or public API surface for this feature scope.
- **FR-011**: System MUST delegate insufficient-input comparison handling (for example, only one plan provided) to the configured Foundry orchestration policy.

### Key Entities *(include if feature involves data)*

- **Conversation Session**: A bounded user interaction context containing user prompts, assistant responses, inferred task intent, and relevant plan references.
- **User Request**: A natural language message that may include one or more intents (comparison, recommendation, general inquiry) and optional constraints.
- **Response Artifact**: Structured assistant output representing a comparison result, recommendation result, clarification prompt, or general answer.
- **Insurance Plan Reference**: A normalized identifier and metadata used to resolve plan-specific user requests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of pilot users complete comparison or recommendation tasks without switching to a separate mode.
- **SC-002**: At least 95% of supported user requests receive a relevant first response in the same chat thread without manual rerouting.
- **SC-003**: At least 85% of follow-up questions in active sessions are correctly answered using prior conversation context.
- **SC-004**: User-reported ease-of-use score for chat task completion improves by at least 30% from pre-change baseline.
- **SC-005**: End-user navigation paths for separate compare/suggest experiences are reduced to zero in the released product surface.

## Assumptions

- Existing plan data and retrieval quality are sufficient to support comparison and recommendation outcomes through one chat surface.
- The feature targets authenticated or standard app users currently able to access chat-based insurance helper functions.
- Existing specialized workflows can be retired immediately without contractual requirement to preserve separate user-visible or API-level compatibility paths.
- Conversation context is retained until explicit user reset and is not automatically expired by inactivity.
- Single-plan comparison handling behavior is governed by Foundry configuration and may be refined during Foundry setup.
