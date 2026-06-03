# digitalOnBoardingV2

A .NET Web API implementing a KYC (Know Your Customer) solution for One Albania, covering digital onboarding, in-store verification, biometric checks, AML/CTF risk screening, digital signatures, and full audit trails.

## Architecture

```
DigitalOnBoardingV2.Api/
  Models/            Domain models, enums, and request DTOs
  Services/          KYC service interfaces and implementations
  Endpoints/         Minimal-API endpoint groups
  Validation.cs      Shared DataAnnotations validation helper
  Program.cs         Dependency injection wiring and app configuration
```

## API Endpoints

### Digital Onboarding
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/onboarding/sessions` | Start a new KYC onboarding session |
| `GET`  | `/api/onboarding/sessions/{id}` | Get session status and results |
| `POST` | `/api/onboarding/sessions/{id}/document` | Submit identity document (OCR + authenticity check) |
| `POST` | `/api/onboarding/sessions/{id}/biometric` | Submit selfie for facial match + liveness; triggers risk assessment |
| `GET`  | `/api/onboarding/sessions/{id}/risk-assessment` | Get AML/CTF risk assessment details |

### In-Store KYC
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/instore/sessions` | Start a staff-initiated in-store KYC session |
| `GET`  | `/api/instore/sessions/{id}` | Get session result |
| `POST` | `/api/instore/sessions/{id}/scan` | Single-call document scan + biometric + risk (instant result) |

### Digital Signatures (eIDAS / UETA aligned)
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/signatures` | Create a signature request (Simple / Advanced / Qualified) |
| `GET`  | `/api/signatures/{id}` | Get signature request status |
| `POST` | `/api/signatures/{id}/sign` | Sign the document (tamper detection included) |
| `GET`  | `/api/signatures/{id}/validate` | Validate completed signature and certificate |

### Audit Trail
| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/api/audit/{sessionId}` | Get full audit trail for a KYC session |

## Onboarding Flow

```
Start Session → Submit Document → Submit Biometric → [Auto risk assessment] → Approved / RequiresReview / Rejected
```
Status values: `0=Pending`, `1=DocumentSubmitted`, `2=BiometricVerified`, `3=RiskAssessed`, `4=Approved`, `5=Rejected`, `6=RequiresReview`

## Run

```bash
dotnet run --project DigitalOnBoardingV2.Api/DigitalOnBoardingV2.Api.csproj
```

## Test

```bash
dotnet test DigitalOnBoardingV2.slnx
```

