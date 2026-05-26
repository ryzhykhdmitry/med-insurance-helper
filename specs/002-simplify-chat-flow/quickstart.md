# Quickstart — Simplify Chat Flow (developer)

1. Ensure backend and frontend build and run locally.

2. Start the backend (from `src/backend/MedInsuranceHelper.Api`):

```powershell
dotnet run --project MedInsuranceHelper.Api
```

3. Start the frontend (from `src/frontend`):

```bash
npm install
npm run start
```

4. Exercise the unified chat endpoint:

```http
POST http://localhost:5000/api/chat
Content-Type: application/json

{
  "message": { "text": "Compare Alpha health plan and Gamma premium plan" }
}
```

5. Confirm the response contains `responseArtifact.sections` with a `comparison` section.

6. To test edge cases, send a compare request with only one plan name and verify the system asks for clarification.
