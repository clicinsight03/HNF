# Healing Natural Farms - Web + MAUI Storefront

A plant nursery + produce shop platform serving two storefronts - **US**
(USD, Stripe + PayPal) and **India** (INR, Razorpay) - from one shared
backend and codebase, targeting .NET 10.

## Status (this delivery)

Built and reviewed so far:

- `HealingNaturalFarms.Domain` - shared entities/enums/DTOs. No external
  dependencies; used by the API, the Blazor web app, and (eventually) the
  MAUI app.
- `HealingNaturalFarms.Payments` - `IPaymentGateway` abstraction with
  Stripe, PayPal, and Razorpay implementations, called directly over
  `HttpClient` against each provider's REST API (no vendor SDK
  dependency). Region -> provider mapping (US: Stripe + PayPal, India:
  Razorpay) is configuration-driven, not hard-coded.
- `HealingNaturalFarms.Infrastructure` - EF Core `AppDbContext` for
  MySQL (via Pomelo), full entity configuration (keys, indexes, decimal
  precision, cascade rules), ASP.NET Core Identity wired to MySQL.
- `HealingNaturalFarms.Api` - JWT auth, region-aware catalog browsing,
  guest + authenticated carts, checkout orchestration (re-validates
  prices/stock server-side, creates the order, hands off to whichever
  gateway the client chose), payment confirmation/verification, device
  registration + push notification sending (Firebase Cloud Messaging,
  covers both Android and iOS from one integration).

Scaffolded but **not yet customized**: `HealingNaturalFarms.Web` (a
default .NET 10 Blazor Web App template) and the `.NET MAUI` project
(not yet created). Those are the next phase - the storefront UI (region
switcher, catalog browsing, cart, checkout screens) and the MAUI app
(same flows + push notification registration) build directly on the API
above.

## An important note on verification

This solution was written and reviewed carefully, but **could not be
compiled in the sandbox this was built in** - that environment's network
policy blocks NuGet entirely (`api.nuget.org` returns 403 through its
egress proxy), so `dotnet restore` fails for any project with package
references. What I *could* verify:

- `HealingNaturalFarms.Domain` and `HealingNaturalFarms.Payments` have
  **zero third-party NuGet dependencies** (everything they use -
  `HttpClient`, `System.Text.Json`, `System.Security.Cryptography` - ships
  in the base class library) and both **build cleanly**, confirmed with
  `dotnet build` in that sandbox.
- Several APIs I assumed were separate NuGet packages turned out to
  already be part of the ASP.NET Core shared framework once installed
  (`Microsoft.AspNetCore.Identity.IdentityUser<T>`,
  `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Options`) - confirmed by probing the compiler
  directly, not assumed.
- `HealingNaturalFarms.Infrastructure` and `HealingNaturalFarms.Api`
  **do** need real NuGet packages (EF Core, the Pomelo MySQL provider,
  JWT bearer auth, Identity's EF store) and could not be build-verified
  here. On your own machine, with normal internet access, `dotnet
  restore` should work with no special setup.

One version constraint worth knowing: **Pomelo's MySQL provider does not
yet have an EF Core 10 release** (current stable is `9.0.0`, targeting EF
Core 9.0.x, per its GitHub releases page). This solution therefore pins
`Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
and `Microsoft.EntityFrameworkCore.Design` to `9.0.9` in the
Infrastructure and Api projects specifically, while everything else
(the TargetFramework itself, ASP.NET Core, JWT bearer, OpenApi) stays on
.NET 10 / 10.0.x - EF Core 9 packages run fine on a net10.0 app. Revisit
this pin once Pomelo ships an EF Core 10 build.

## Local setup

1. **MySQL**: create a database and an app user, e.g.:
   ```sql
   CREATE DATABASE healingnaturalfarms;
   CREATE USER 'hnf_app'@'%' IDENTIFIED BY 'change-me';
   GRANT ALL PRIVILEGES ON healingnaturalfarms.* TO 'hnf_app'@'%';
   ```
   Update `src/HealingNaturalFarms.Api/appsettings.json` ->
   `ConnectionStrings:Default` to match (or better, override it via user
   secrets / environment variables rather than editing the checked-in
   file - see Secrets below).

2. **Restore + build**:
   ```bash
   cd HealingNaturalFarms
   dotnet restore
   dotnet build
   ```

3. **Create the initial migration and apply it** (from the repo root):
   ```bash
   dotnet tool install --global dotnet-ef   # one-time, if you don't have it
   dotnet ef migrations add InitialCreate \
     --project src/HealingNaturalFarms.Infrastructure \
     --startup-project src/HealingNaturalFarms.Api
   dotnet ef database update \
     --project src/HealingNaturalFarms.Infrastructure \
     --startup-project src/HealingNaturalFarms.Api
   ```

4. **Run the API**:
   ```bash
   dotnet run --project src/HealingNaturalFarms.Api
   ```

## Secrets - never commit real keys

`appsettings.json` ships with obvious `CHANGE_ME` placeholders for:

- **Jwt:SigningKey** - any long random string (32+ bytes).
- **Payments:Stripe** - get test keys from the Stripe dashboard
  (Developers > API keys) and a webhook signing secret if you wire up
  webhooks.
- **Payments:PayPal** - a sandbox app's Client ID/Secret from the PayPal
  Developer Dashboard.
- **Payments:Razorpay** - test Key ID/Secret from the Razorpay dashboard.
- **Firebase** - a Firebase project's `ProjectId`, and a service account
  JSON key file (Firebase Console > Project Settings > Service Accounts
  > Generate new private key) - point `ServiceAccountJsonPath` at it and
  keep the file itself out of source control (it's in `.gitignore`
  already via the `firebase-service-account*.json` pattern).
- **Email** - SMTP credentials for sending the order-confirmation/invoice
  email (see "Order confirmation emails" below). Works with any SMTP
  provider - Gmail SMTP, or the SMTP relay endpoint of SendGrid, Mailgun,
  Postmark, AWS SES, etc.

For local dev, prefer `dotnet user-secrets` over editing
`appsettings.json` directly:
```bash
cd src/HealingNaturalFarms.Api
dotnet user-secrets init
dotnet user-secrets set "Payments:Stripe:SecretKey" "sk_test_..."
```

## Order confirmation emails

Every successfully paid order (US or India, any of the three gateways,
guest or signed-in) triggers an email to the customer's checkout email
address: `IOrderEmailSender` (`src/HealingNaturalFarms.Api/Services/OrderEmailSender.cs`),
sent from `CheckoutService.ConfirmPaymentAsync` right after the order is
marked `Paid`. The email includes the order/invoice number
(`order.OrderNumber`, e.g. `HNF-US-482913`), every line item with its
quantity, unit price and line total, the subtotal/shipping/tax/total
breakdown, and the shipping address - both an HTML view (what most
inboxes render) and a plain-text fallback.

It's sent over plain SMTP via the .NET base class library's
`System.Net.Mail.SmtpClient` - deliberately not a NuGet mail package,
for the same reason the payment gateways use raw `HttpClient` instead of
vendor SDKs: this sandbox has no NuGet access to verify one against, and
`SmtpClient` needs nothing beyond credentials to work with any SMTP
provider. Configure it via the `Email` section in `appsettings.json` (or
`dotnet user-secrets`, same as the other secrets above):

```json
"Email": {
  "SmtpHost": "smtp.yourprovider.com",
  "SmtpPort": 587,
  "EnableSsl": true,
  "Username": "your-smtp-username",
  "Password": "your-smtp-password",
  "FromEmail": "orders@healingnaturalfarms.com",
  "FromName": "Healing Natural Farms"
}
```

Sending is best-effort and never blocks or fails the checkout response -
same contract as the push-notification send right above it in
`CheckoutService`: if the mail server is unreachable or misconfigured,
the order is still correctly saved as paid, and the failure is swallowed
rather than surfaced to the shopper. If you later want a more actively
maintained mail client (connection pooling, retries, DKIM helpers) once
you have NuGet access, MailKit's `SmtpClient` is a drop-in swap behind
the same `IOrderEmailSender` interface - nothing else in the app needs to
change. A PDF invoice attachment is a natural next step but wasn't added
here to avoid pulling in a PDF-generation package sight-unseen; the HTML
email already contains everything a PDF invoice would.

## Opening in VS Code

Open the `HealingNaturalFarms` folder directly. Install the "C# Dev Kit"
extension if you don't have it. `dotnet build` from the integrated
terminal is the fastest way to confirm everything restores correctly on
your machine before you start customizing the Blazor storefront or
adding the MAUI project.
