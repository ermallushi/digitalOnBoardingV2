# digitalOnBoardingV2

A minimal .NET Web API for managing One Albania KYC RFI requirements across digital onboarding, in-store verification, and digital signatures.

## Endpoints

- `GET /api/rfi` - returns the structured RFI requirements and objectives.
- `GET /api/rfi/vendor-responses` - lists submitted vendor responses.
- `POST /api/rfi/vendor-responses` - submits a vendor capability response.

## Run

```bash
dotnet run --project DigitalOnBoardingV2.Api/DigitalOnBoardingV2.Api.csproj
```

## Test

```bash
dotnet test DigitalOnBoardingV2.slnx
```
