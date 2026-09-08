# Reintegration Guide

This document explains how to bring this proof-of-concept back into a **host repository / workspace** (for example the
original parent codebase this was extracted from) when it is needed again. It is written for both a **human** following
steps manually and an **agent** that has been asked to perform the integration.

All content in this repository is **generic and vendor-neutral**. There are no organisation-specific names, teams or
subscription references, so it can be placed into any host without renaming anything that belongs to the host.

---

## 1. What is being moved

| Source folder (this repo) | Typical host destination | Content |
|---|---|---|
| `email-architecture-comparison/` | `<host>/poc/email-architecture-comparison/` | Main demo: .NET solution, docs, infra |
| `email-docs/` | `<host>/poc/email-docs/` (or under the host's docs) | Architecture analysis & design docs |
| `transactional-email-poc/` | `<host>/poc/transactional-email-poc/` | Early provider POC code |

The host `.gitignore` already excludes a `poc/` directory in the original workspace, so placing the folders under a
similarly ignored `poc/` path keeps them out of the host's tracked tree.

---

## 2. Manual steps (human)

### 2.1 Copy the folders into the host workspace

```powershell
# example only — change paths as needed
$hostRoot = "C:\Dev\<host-workspace>"
$repoRoot = (Get-Location).Path

Copy-Item -Recurse -Force "$repoRoot\email-architecture-comparison" "$hostRoot\poc\email-architecture-comparison"
Copy-Item -Recurse -Force "$repoRoot\email-docs"                "$hostRoot\poc\email-docs"
Copy-Item -Recurse -Force "$repoRoot\transactional-email-poc"   "$hostRoot\poc\transactional-email-poc"
```

### 2.2 Build & run the main demo

```powershell
cd "$hostRoot\poc\email-architecture-comparison"
dotnet build EmailArchitectureComparison.slnx
dotnet run --project src/TransactionalEmail.CentralApi
```

Health check: `GET http://localhost:5080/health` should return `{"status":"ok","provider":"mandrill","mode":"simulation"}`.

Requirements:
- .NET 10 SDK (Central API, Client, Mandrill, SharedLibraryDemo) and the .NET 8 targeting pack (AuditFunctions worker).
- No external Azure resources are required for the local simulation mode.
- To run a real send, set `MANDRILL_API_KEY`, `FROM_EMAIL`, `DEMO_TO_EMAIL` as environment variables only.

### 2.3 (Optional) Add to a host solution

If the main demo should appear in a host `.sln`/`.slnx`, reference the projects below from the host projects, but **do
not** copy email-related logic into host business code — keep the boundary as a reference / client call:

```powershell
dotnet sln <host-solution> add src/TransactionalEmail.Contracts/TransactionalEmail.Contracts.csproj \
    src/TransactionalEmail.Client/TransactionalEmail.Client.csproj \
    src/TransactionalEmail.Mandrill/TransactionalEmail.Mandrill.csproj \
    src/TransactionalEmail.CentralApi/TransactionalEmail.CentralApi.csproj \
    src/TransactionalEmail.SharedLibraryDemo/TransactionalEmail.SharedLibraryDemo.csproj
```

### 2.4 (Optional) Keep the folder as a separate git history

To keep the original commit history instead of a plain file copy, the host repo can pull this repository's history in
under a prefix:

```powershell
# from the host repo root
git remote add transactional-email https://github.com/shared-vs-central-service-architecture.git
git fetch transactional-email

# bring the main demo in with its full history
git subtree add --prefix=poc/email-architecture-comparison transactional-email/main --squash
```

Squashing keeps the host repo small; omit `--squash` only if the full POC history must be visible in the host.

---

## 3. Agent instructions (ready to paste)

> You are asked to bring the standalone repository `https://github.com/preethamkh/transactional-email` into the current
> host workspace as a clearly separated POC, without touching any part of the host's production code.
>
> 1. Clone `https://github.com/preethamkh/transactional-email.git` into a temporary folder (or add it as a remote
>    `transactional-email` and fetch).
> 2. Copy three folders into `<host>/poc/` as separate, clearly-named folders:
>    - `email-architecture-comparison` (the main .NET demo),
>    - `email-docs` (the architecture/design docs),
>    - `transactional-email-poc` (the early provider POC code).
> 3. Confirm the POC folder path is covered by the host's `.gitignore` (`poc/`) so it stays out of the tracked tree, or
>    add it if missing.
> 4. From `poc/email-architecture-comparison`, run `dotnet build EmailArchitectureComparison.slnx` and verify it
>    succeeds, then start the API and confirm `GET http://localhost:5080/health` returns `mode: simulation`.
> 5. Do not modify any host source files, do not add the POC projects to the host solution unless explicitly asked, do
>    not commit secrets, and do not change the POC folder contents beyond what is needed to make the build pass.
> 6. If a real send is required later, set `MANDRILL_API_KEY`, `FROM_EMAIL` and `DEMO_TO_EMAIL` as environment
>    variables only.

---

## 4. Notes & cautions

- The POC content is intentionally **independent of any organisation**; treat it as personal IP and don't attribute it to
  a corporate team or system.
- The demo defaults to `simulation` mode and deliberately does **not** send email unless a Mandrill key is supplied.
- Azure Terraform under `email-architecture-comparison/infra/` targets a **personal subscription only**; it is never
  applied automatically and never references any organisation subscription.
- Do not copy `bin/`, `obj/` or `.deploy/` build artifacts into the host — they are gitignored here and irrelevant.