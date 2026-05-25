# BlackChannel

> **Note:** BlackChannel is an **engineering exercise in end-to-end encryption** and a
> **portfolio project** — built to demonstrate a server-blind E2EE architecture on Azure,
> done properly and in the open. It's real, working software you can run, not a hosted
> commercial service. Use it, learn from it, fork it.

**Private messages your mates can read — and nobody in between can. Not even the people running the server.**

BlackChannel is an open-source, end-to-end encrypted (E2EE) messaging service you can
**deploy to your own Azure account in a few minutes**. Messages are encrypted in your
browser before they ever leave your device; the server only ever stores and forwards
scrambled ciphertext. It can't read your messages, and neither can we — because with
this repo, *you* are the operator.

It's the same kind of privacy that ships in apps hundreds of millions of people use
every day — for families, friends, professionals handling confidential information, and
anyone who simply prefers their private conversations stay private. See
[**what it's for and acceptable use**](docs/00-what-this-is.md).

- 🔒 **End-to-end encrypted** — ECDH + AES-256-GCM, performed client-side, fresh key per message.
- 🙈 **Server is blind** — no plaintext, no private keys ever reach it.
- 🔗 **Invite a mate with a link** — no directory, no phone numbers.
- ⚡ **Realtime** — Azure SignalR pushes encrypted blobs instantly.
- 💸 **Cheap** — Flex Consumption Functions + a Free Static Web App; scales to ~zero when idle.
- 🔓 **Verifiable** — read the crypto yourself, or just run your own copy and trust no operator.

---

## Deploy your own

You'll need: an **Azure account** (a free/pay-as-you-go subscription is fine) and a
**GitHub account**. Everything sits on free/consumption tiers and scales to ~zero when
idle; pass a `budgetEmail` to the template if you want a monthly cost-alert.

### Step 1 — Create the Azure resources

Click the button. It provisions everything (storage, SignalR, the Function App, the
Static Web App) into a resource group of your choice. Nothing here costs money at rest.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FHayesBD%2FBlackChannel%2Fmain%2Finfra%2Fazuredeploy.json)

> Prefer the CLI? `az deployment group create -g <your-rg> --template-file infra/main.bicep`.

### Step 2 — Fork this repo

Click **Fork** (top-right). Your fork is where the app code gets built and deployed from.

### Step 3 — Connect your fork to your Azure account (one-time)

The deploy uses GitHub's OIDC login to Azure — **no secrets/passwords stored**. Create an
app registration and federated credential (copy-paste, ~2 min):

```bash
# Sign in and pick your subscription
az login
az account set --subscription "<your-subscription-id>"

# Create an app registration for the deploy
appId=$(az ad app create --display-name "blackchannel-deploy" --query appId -o tsv)
az ad sp create --id "$appId"

# Let it deploy into your resource group (Contributor on the RG you made in Step 1)
subId=$(az account show --query id -o tsv)
az role assignment create --assignee "$appId" --role Contributor \
  --scope "/subscriptions/$subId/resourceGroups/<your-rg>"

# Trust GitHub Actions on your fork's main branch
az ad app federated-credential create --id "$appId" --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<your-github-username>/BlackChannel:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

echo "AZURE_CLIENT_ID=$appId"
echo "AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID=$subId"
```

Then in your fork: **Settings → Secrets and variables → Actions → New repository secret**,
and add the three values printed above:

| Secret | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | the `appId` |
| `AZURE_TENANT_ID` | your tenant id |
| `AZURE_SUBSCRIPTION_ID` | your subscription id |

If your resource group name isn't `blackchannel`, edit `AZURE_RG` at the top of
`.github/workflows/deploy.yml`.

### Step 4 — Deploy

Push any change to `main` (or **Actions → deploy → Run workflow**). The workflow builds
the Functions backend and the Blazor site and deploys both. It prints your site URL in
the run summary. Done — that's your own private BlackChannel.

> Want real sign-in instead of the local dev fallback? Create a
> [Microsoft Entra External ID](https://learn.microsoft.com/entra/external-id/customers/) tenant
> (free tier), register a SPA, then set `ENTRA_AUTHORITY` + `ENTRA_AUDIENCE` on the Function
> app and `entra:authority`/`entra:clientId` in `wwwroot/appsettings.json`.

---

## Run it locally

Prerequisites: **.NET 8 SDK**, **Node.js** (for Azurite), **Azure Functions Core Tools v4**
(`func`), and the **Azure SignalR local emulator** (realtime delivery needs it):

```bash
npm install -g azurite
dotnet tool install -g Microsoft.Azure.SignalR.Emulator
```

One-time: copy the Functions settings template (the real file is gitignored):

```bash
cp src/BlackChannel.Functions/local.settings.json.example src/BlackChannel.Functions/local.settings.json
```

Then run these in four terminals:

```bash
# 1. Local Azure Storage
azurite --silent --location ./azurite-data

# 2. Local SignalR (the connection string in local.settings.json.example points here)
asrs-emulator start

# 3. Backend — Functions host on http://localhost:7071
cd src/BlackChannel.Functions && func start

# 4. Frontend — Blazor on http://localhost:5173 (appsettings.Development.json points it at :7071)
dotnet run --project src/BlackChannel.Web
```

Open http://localhost:5173. Auth runs in dev-user mode locally (no sign-in needed); each
browser gets its own identity, so open a second browser to message yourself via an invite.

## How it works

| Project | What it is |
| --- | --- |
| `src/BlackChannel.Shared`    | DTOs shared by client + server (sealed envelopes, key bundles, invites). |
| `src/BlackChannel.UI`        | Shared Razor UI + client services + crypto — used by both the web and native apps. |
| `src/BlackChannel.Functions` | Azure Functions (.NET 8 isolated, Flex Consumption). The blind mailbox. |
| `src/BlackChannel.Web`       | Blazor WebAssembly site (PWA). Hosts the shared UI. |
| `src/BlackChannel.App`       | MAUI native app (Android + Windows) — same shared UI, installable off-store. |

Start with [`docs/00-what-this-is.md`](docs/00-what-this-is.md). The encryption
specifically: [`docs/02-e2ee-crypto.md`](docs/02-e2ee-crypto.md).

## Security, honestly

E2EE means **we (or you, as operator) cannot recover your messages or reset your keys.**
Lose your device key and the messages encrypted to it are gone — that's the trade-off for
real privacy. And no app can protect a device that's already been compromised by spyware;
keep yours patched. Details: [`docs/02-e2ee-crypto.md`](docs/02-e2ee-crypto.md).

## License & disclaimer

Provided as-is, no warranty. For lawful, private communication — you're responsible for
complying with the laws that apply to you. See
[acceptable use](docs/00-what-this-is.md#acceptable-use).
