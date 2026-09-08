# Transactional Email

A small, standalone proof-of-concept workspace that explores transactional email architecture options.
This repository is **independent and vendor-neutral**. It is a personal design/learning space, kept free to be
re-used or re-integrated into any host workspace if needed.

## Repository layout

| Folder | What it is | Where to start |
|---|---|---|
| [`email-central-service/`](email-central-service/docs/ARCHITECTURE-COMPARISON.md) | **NEW**: Central email service microservice with full architecture comparison docs, Terraform, and demo | `docs/ARCHITECTURE-COMPARISON.md` |
| [`email-shared-library-host/`](email-shared-library-host/) | **NEW**: Shared library approach demo showing the pain points of distributed sending | `Program.cs` |
| [`email-architecture-comparison/`](email-architecture-comparison/README.md) | The main demo: shared-library (distributed) sending vs a **central email service**, with a thin client, audit/support view, Mandrill adapter and Terraform for an optional demo deployment | its own `README.md` |
| [`email-docs/`](email-docs/README.md) | Analysis and design documentation: options paper, central-service recommendation, POC strategy, run/demo guide and integration notes | its own `README.md` |
| [`transactional-email-poc/`](transactional-email-poc/README.md) | Early provider POC code: Mailchimp template retrieval (Option 2) and the central email service spike (Option 4), plus `demo.http` | `email-option2-mailchimp/README.md` and `email-option4-central-service/README.md` |

## Quick Start — Run Both Approaches Locally

```powershell
# Terminal 1: Shared Library (Assessment app) — Port 5090
dotnet run --project email-shared-library-host/src/SharedLibrary.Host

# Terminal 2: Central Service — Port 5080
dotnet run --project email-central-service/src/CentralApi
```

Then open:
- http://localhost:5090/ — Shared Library UI (Assessment emails ONLY)
- http://localhost:5080/ — Central Service UI (ALL systems — unified view)

Use the `demo.http` files in each project with VS Code REST Client to send test requests.

## Key Findings

### Shared Library Approach — Pain Points
- Mandrill API key duplicated in every app's config (secret sprawl)
- Each app has its own audit store — support must query each one separately
- Each app handles Mandrill webhooks independently (duplicated code)
- Template mapping duplicated across apps
- **D365 and Power Automate cannot use a .NET library** — they need HTTP anyway
- No central correlation ID or unified support view
- Multiple DB hits for template mapping lookups

### Central Service Approach — Benefits
- One set of Mandrill credentials (stored centrally)
- One audit store (unified support view)
- One webhook handler
- Central template registry
- D365 and Power Automate use the same HTTP API
- End-to-end correlation IDs across send → webhook → audit → CRM
- ~50-65% lower cost at production scale

## Azure Deployment

### Existing Resources (Already Provisioned)
The `rg-email-architecture-lab` resource group has:
- App Service (F1 free) — `email-arch-lab-api`
- Function App (consumption) — `email-arch-lab-fn`
- Service Bus (Basic) — `emailarchlabnamespace`
- Storage (Standard LRS) — `emailarchlabstore`

### Cost Estimate
| Resource | Tier | Cost |
|---|---|---|
| App Service (×2) | F1 (Free) | $0 |
| Service Bus | Basic | ~$0-0.50/mo |
| Function App | Consumption | $0 (1M free) |
| Storage | Standard LRS | ~$0.02/mo |
| **Total** | | **~$0-0.50/month** |

## Detailed Documentation

See `email-central-service/docs/ARCHITECTURE-COMPARISON.md` for:
- Full architecture diagrams (Mermaid)
- Sequence diagrams showing data flow
- Detailed pain point analysis
- Cost comparison
- Production deployment guide

## Re-integrating into another workspace

If this POC needs to be placed inside a parent solution/workspace of a larger solution, see
[`docs/REINTEGRATION-GUIDE.md`](docs/REINTEGRATION-GUIDE.md). It contains both step-by-step manual instructions and a
ready-to-use agent prompt.
