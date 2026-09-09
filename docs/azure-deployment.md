# Hosting Healing Natural Farms on Azure

This maps the current codebase (Blazor Web App + API on .NET 10, MySQL via
Pomelo/EF Core, MAUI mobile client, Stripe/PayPal/Razorpay payments, SMTP
order-confirmation emails) onto Azure services, and gives the concrete
provisioning steps. The MAUI app itself isn't "hosted" anywhere — it ships
through the Play Store / App Store — what Azure hosts is its backend API
plus the Blazor storefront.

## What you need

- An Azure subscription with Contributor access (Owner if you'll also
  assign roles/managed identities).
- **Azure Database for MySQL – Flexible Server**: replaces local MySQL.
  `database/mysql/schema.sql` (just added to the repo) runs against it as-is.
- **Azure App Service (Linux, .NET 10)**: hosts `HealingNaturalFarms.Api`
  (which also serves the Blazor Web/Web.Client via the existing hosting
  model). One Web App per environment (Dev/QA/Prod) maps naturally onto
  the DEV/QA/PROD git branches you already have.
- **Azure Key Vault**: SMTP password, Stripe secret key, PayPal
  client secret, Razorpay key secret, the MySQL admin/app password, and
  the JWT signing key — none of these belong in `appsettings.json` or in
  plain App Service settings once this is live.
- **Azure Blob Storage**: `ProductImage.Url` is just a string column today
  (images aren't bundled in the repo) — point it at blob storage
  (optionally behind a CDN) once you have real product photography.
- **Application Insights**: request/exception telemetry and log search
  (requires adding the `Microsoft.ApplicationInsights.AspNetCore` package
  — a small code change, not just a portal setting).
- **A custom domain + TLS certificate** (App Service's free managed
  certificate covers this).
- **GitHub Actions**, since the repo is already on GitHub with DEV/QA/PROD
  branches — one workflow per branch deploying to the matching Web App.
- Optionally, **Azure Notification Hubs** for the MAUI push notifications
  — see the note at the bottom.

## Step-by-step

### 1. Resource group
```bash
az group create --name hnf-rg --location eastus
```

### 2. MySQL – Azure Database for MySQL Flexible Server
```bash
az mysql flexible-server create \
  --resource-group hnf-rg \
  --name hnf-mysql \
  --location eastus \
  --admin-user hnfadmin \
  --admin-password '<strong-password>' \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 8.0.21 \
  --storage-size 32 \
  --high-availability Disabled
```
Then open the firewall to your own IP (and later to App Service's
outbound IPs, or use a VNet integration instead of public firewall rules
for production), create the `healingnaturalfarms` database, and run the
script that's now in the repo:
```bash
az mysql flexible-server firewall-rule create \
  --resource-group hnf-rg --name hnf-mysql \
  --rule-name allow-my-ip --start-ip-address <your-ip> --end-ip-address <your-ip>

mysql -h hnf-mysql.mysql.database.azure.com -u hnfadmin -p \
  < database/mysql/schema.sql
```
Azure MySQL enforces TLS by default — the connection string needs
`SslMode=Required;` added (Pomelo/MySqlConnector both support this
directly; no extra CA file is normally required for the default Azure
managed certificate chain).

### 3. App Service — one per environment
```bash
az appservice plan create --resource-group hnf-rg --name hnf-plan --is-linux --sku B1

for env in dev qa prod; do
  az webapp create --resource-group hnf-rg --plan hnf-plan \
    --name hnf-api-$env --runtime "DOTNETCORE:10.0"
done
```
Bump the Prod plan to at least `P0v3` once you're past initial testing —
`B1` has no autoscale and is fine for Dev/QA only.

### 4. App settings per environment
For each Web App, set (via CLI, Bicep/ARM, or the portal):
- `ConnectionStrings__Default` — the MySQL connection string with
  `SslMode=Required`
- `Email__SmtpHost`, `Email__Username`, `Email__Password`, etc. (matches
  `EmailOptions` in `OrderEmailSender.cs`)
- Payment provider keys (Stripe/PayPal/Razorpay) matching whatever
  configuration keys `HealingNaturalFarms.Payments` reads today
- `ASPNETCORE_ENVIRONMENT` = `Development` / `Staging` / `Production`

Anything secret should be a **Key Vault reference**
(`@Microsoft.KeyVault(SecretUri=...)`) rather than a literal value —
that requires enabling the Web App's system-assigned managed identity and
granting it the `Key Vault Secrets User` role on the vault.

### 5. Key Vault
```bash
az keyvault create --resource-group hnf-rg --name hnf-kv --location eastus
az webapp identity assign --resource-group hnf-rg --name hnf-api-prod
az role assignment create --assignee <principal-id-from-above> \
  --role "Key Vault Secrets User" --scope <key-vault-resource-id>
```

### 6. Blob Storage for product images (when you have real images)
```bash
az storage account create --resource-group hnf-rg --name hnfmedia --sku Standard_LRS
az storage container create --account-name hnfmedia --name product-images --public-access blob
```
Point `ProductImage.Url` rows at the resulting blob URLs (or a CDN
endpoint in front of the storage account).

### 7. CI/CD — GitHub Actions per branch
Add `.github/workflows/deploy-{dev,qa,prod}.yml`, each triggered on push
to the matching branch, using `azure/webapps-deploy@v3` with a publish
profile or OIDC federated credential (preferred over long-lived
publish-profile secrets) pointed at that environment's Web App name.

### 8. Custom domain + TLS
```bash
az webapp config hostname add --webapp-name hnf-api-prod --resource-group hnf-rg \
  --hostname shop.healingnaturalfarms.com
az webapp config ssl create --resource-group hnf-rg --name hnf-api-prod \
  --hostname shop.healingnaturalfarms.com
```

### 9. Application Insights
```bash
az monitor app-insights component create --resource-group hnf-rg \
  --app hnf-insights --location eastus --application-type web
```
Wire the connection string in as an app setting and add
`builder.Services.AddApplicationInsightsTelemetry();` to `Program.cs`
(not yet in the codebase — small addition, flagging it here rather than
silently treating it as already done).

### 10. US/India: one deployment is enough to start
The US/India split in this app is a data/region concept
(`RegionCode`, `ProductRegionListing`, `Currency`) inside a single
codebase and database — it does **not** require two separate
deployments. One App Service + one MySQL server serving both storefronts
is the right starting point. If India-side latency becomes a real
problem later, the scale-out path is a second App Service in an India
region behind Azure Front Door / Traffic Manager, still against the same
(or a read-replica) MySQL server — worth deferring until it's actually
needed.

## Note on push notifications and Azure

`PushTokenService.cs` currently documents a gap: it needs real Firebase
(Android) and APNs+Firebase (iOS) integration to retrieve a device token.
**Azure Notification Hubs** is a reasonable alternative to wiring FCM and
APNs credentials directly into `HealingNaturalFarms.Api`: it lets the
backend send through one SDK/API and Notification Hubs fans out to FCM
and APNs using credentials configured once in the Hub, rather than the
app managing both providers' credentials itself. If you go this route,
note that Notification Hubs' **legacy FCM API is being retired** — any
new Android integration should use the FCM v1 API path from the start.
This is an alternative to, not a requirement for, the current
Firebase/APNs-direct design already documented in the MAUI project.

## Rough cost shape (not a quote — check current Azure pricing for your region)

- MySQL Flexible Server, Burstable B1ms: cheapest tier, fine for Dev/QA
  and light Prod traffic; move to General Purpose once real traffic hits.
- App Service Plan: B1 for Dev/QA, P0v3+ for Prod (autoscale, no cold
  starts).
- Blob Storage + Key Vault + Application Insights: all usage-based and
  small relative to the above two.
