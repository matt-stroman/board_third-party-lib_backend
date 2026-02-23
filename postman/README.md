# Postman API Testing Assets

This folder contains versioned Postman assets for testing the backend API-first endpoints.

## Contents

- [`collections/BoardThirdPartyLibrary.Api.postman_collection.json`](collections/BoardThirdPartyLibrary.Api.postman_collection.json)
- [`environments/local.postman_environment.json`](environments/local.postman_environment.json)

## Goals

- Keep request definitions versioned in source control
- Keep response assertions (tests) data-/code-driven
- Support both Postman GUI usage and future CLI/CI execution via Newman

## Current Coverage

- `GET /`
- `GET /health/live`
- `GET /health/ready`

## Usage (Postman GUI)

1. Import the collection JSON file.
2. Import the local environment JSON file.
3. Select the `Board Third Party Library - Local` environment.
4. Start the backend API locally.
5. Run the collection or individual requests.

## Environment-Driven Assertions

The collection reads expectations from environment variables so you can reuse the same requests/tests in multiple environments:

- `baseUrl`
- `expectedServiceName`
- `expectedReadyHttpStatus`
- `expectedReadyStatus`
- `expectedReadyDatabase`
- `expectedReadyUser`

For example, if you intentionally test a degraded environment, you can set:

- `expectedReadyHttpStatus=503`
- `expectedReadyStatus=Unhealthy`

## Optional CLI Runner (Newman)

Use the backend helper script:

- [`backend/scripts/run-postman.ps1`](../scripts/run-postman.ps1)

Or run Newman manually:

```powershell
npx newman run .\backend\postman\collections\BoardThirdPartyLibrary.Api.postman_collection.json -e .\backend\postman\environments\local.postman_environment.json
```

## Versioning Guidance

- Commit shared collections and non-secret environments.
- If you create personal/private environments (custom ports, tokens, etc.), do not commit them.
- Prefer updating tests in the collection when endpoint contracts change so regression checks stay close to the requests.
