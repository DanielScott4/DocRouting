# PadesSign

PAdES-B-LT compliant PDF electronic signature routing system built with .NET 8.

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 8.0+ |
| Docker Desktop | 4.x |
| Chrome or Edge | Latest |
| Smartcard middleware | SafeNet / Gemalto / OpenSC |

## Quick start (local dev)

```bash
# 1. Clone and enter the repo
git clone https://github.com/your-org/PadesSign && cd PadesSign

# 2. Start SQL Server + Azurite (local blob storage)
docker compose up db azurite -d

# 3. Run the API (applies EF migrations automatically)
cd src/PadesSign.Api && dotnet run

# 4. Run the Blazor client
cd ../PadesSign.Web && dotnet run

# 5. Install the smartcard native messaging host (Windows, run as Admin)
cd ../../native-host/SmartcardHost
dotnet publish -r win-x64 -c Release
cd bin/Release/net8.0/win-x64/publish
powershell -File ../../../../install-host-windows.ps1
```

Open https://localhost:5001 (Blazor) and https://localhost:5000/swagger (API).

## Architecture

```
Browser (Blazor WASM)
  â””â”€ PDF.js viewer + signature field overlays
  â””â”€ smartcardBridge.js  â”€â”€â–¶  Chrome extension  â”€â”€â–¶  SmartcardHost (.NET)
                                                         â””â”€ PKCS#11 â”€â”€â–¶ Smartcard
  â””â”€ SignalR client

ASP.NET Core 8 API
  â”œâ”€ DocumentsController    (upload, download, stream)
  â”œâ”€ SigningController       (prepare / finalize)
  â”œâ”€ WorkflowTemplatesController
  â””â”€ SigningHub (SignalR)

Infrastructure
  â”œâ”€ PadesSigningService    (iText 7, PAdES-B-LT)
  â”œâ”€ WorkflowOrchestrator   (routing, parallel steps)
  â”œâ”€ AzureBlobStorage       (PDFs)
  â””â”€ EF Core + SQL Server   (workflow definitions, audit log)
```

## PAdES conformance

The system produces **PAdES-B-LT** PDFs:
- Signature embedded as CMS/PKCS#7 detached
- RFC 3161 timestamp from configured TSA
- OCSP responses + CRL snapshots in the PDF DSS dictionary
- Incremental saves (append-only; prior signatures never modified)

Upgrade to PAdES-B-LTA by scheduling periodic archive re-timestamping.

## Workflow templates

Templates define ordered, optionally parallel signing steps via the REST API:

```json
POST /api/workflow-templates
{
  "name": "Three-party approval",
  "steps": [
    { "order": 1, "assigneeId": "...", "isParallel": false,
      "pageNumber": 1, "x": 60, "y": 700, "width": 180, "height": 60,
      "reason": "Legal review", "location": "London" },
    { "order": 2, "assigneeId": "...", "isParallel": true,
      "pageNumber": 1, "x": 260, "y": 700, "width": 180, "height": 60,
      "reason": "CFO approval", "location": "New York" },
    { "order": 2, "assigneeId": "...", "isParallel": true,
      "pageNumber": 1, "x": 460, "y": 700, "width": 180, "height": 60,
      "reason": "CEO approval", "location": "New York" }
  ]
}
```

Steps with the same `order` run in parallel and both must sign before advancing.

## Smartcard setup

1. Install your cards PKCS#11 middleware (SafeNet, Gemalto, OpenSC, etc.)
2. Publish `SmartcardHost` as a self-contained executable
3. Register the native messaging host manifest in the OS registry / file system
4. Install the companion Chrome/Edge extension (update `YOUR_EXTENSION_ID_HERE` in `smartcardBridge.js`)

## iText 7 licensing

iText 7 Community is **AGPL-3.0**. If you are building a closed-source or SaaS product,
you need a commercial iText licence. See https://itextpdf.com/pricing.

## Environment variables (production)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Azure__Storage__ConnectionString` | Azure Blob Storage / Azurite |
| `Pades__TsaUrl` | RFC 3161 TSA endpoint (e.g. https://freetsa.org/tsr) |
| `Pades__TsaLogin` / `TsaPassword` | TSA credentials if required |
| `SendGrid__ApiKey` | Outbound email for notifications |
| `Jwt__Authority` | OIDC authority URL |
| `SMARTCARD_PIN` | (Native host only) fallback PIN â€” replace with GUI prompt in production |

## Running EF Core migrations manually

```bash
cd src/PadesSign.Api
dotnet ef migrations add InitialCreate --project ../PadesSign.Infrastructure
dotnet ef database update
```