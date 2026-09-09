# Healing Natural Farms - Web + MAUI Storefront

A plant nursery + produce shop platform serving two storefronts - **US**
(USD, Stripe + PayPal) and **India** (INR, Razorpay) - from one shared
backend and codebase, targeting .NET 10.

## Status (this delivery)

Built and reviewed so far:

- `HealingNaturalFarms.Domain` - shared entities/enums/DTOs. No external
  dependencies; used by the API, the Blazor web app, and the MAUI app.
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
  covers both Android and iOS from one integration), order-confirmation
  invoice emails (see "Order confirmation emails" below).
- `HealingNaturalFarms.Web` - the Blazor Web App storefront (Interactive
  Auto). Home, Shop (catalog with category/search/pagination), Product
  Detail, Cart, Checkout (Stripe Payment Element / PayPal Buttons /
  Razorpay Checkout all wired to the API), Login/Register, Order
  Confirmation, and a full `hnf-*` CSS theme.
- `HealingNaturalFarms.Maui` - the same storefront flows as a native
  Android/iOS app: Shop, Product Detail, Cart, Checkout, Login/Register,
  Order Confirmation, region switcher, and push-notification
  registration plumbing (see "The MAUI app" below for what's real vs.
  documented-gap).

Not built: nothing from the original brief - everything above is at
least a first, careful pass. What's genuinely incomplete is documented
inline rather than silently skipped: a PDF invoice attachment, real
Firebase/APNs push token retrieval, and app icon/font artwork (all
noted at their point of use, with exactly what to add).

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
- `HealingNaturalFarms.Web.Client` (the Blazor WebAssembly project) and
  `HealingNaturalFarms.Maui` also couldn't be build-verified - the
  former needs a NuGet-restored WebAssembly package, and the latter
  needs the `maui-android`/`maui-ios`/`maui-maccatalyst` workloads
  (`dotnet workload install maui` also failed here: this sandbox is a
  plain Linux container with no Android/iOS SDKs, and workload install
  itself needs the same blocked NuGet access). Every Razor page and
  every MAUI XAML file was instead checked by hand: DTO/enum field names
  cross-referenced against the API, JS interop call signatures matched
  argument-by-argument against `payments.js`/`checkout.html`, and every
  `.xaml` file specifically re-validated as well-formed XML with a
  script (which did catch one real bug - a `<!-- ---- ... ---- -->`
  style comment with doubled hyphens, invalid per the XML spec, in
  `Resources/Styles/Styles.xaml`). This is a real substitute for reading
  the code carefully, but it is not the same guarantee a compiler gives
  you - budget time for a first build-and-click-through on your own
  machine before trusting this deeply.

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
   CREATE DATABASE hnf;
   CREATE USER 'hnf_app'@'%' IDENTIFIED BY 'change-me';
   GRANT ALL PRIVILEGES ON hnf.* TO 'hnf_app'@'%';
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

## The MAUI app

`src/HealingNaturalFarms.Maui` is a hand-authored .NET MAUI project
(`dotnet new maui` isn't available in this sandbox - no workload, so the
standard template structure was written out by hand: csproj,
`MauiProgram.cs`, `App`/`AppShell`, and the minimal `Platforms/{Android,
iOS, MacCatalyst}` boilerplate every MAUI app needs). It targets
`net10.0-android`, `net10.0-ios`, and `net10.0-maccatalyst` - Windows was
left out since the brief was Android + iOS specifically; add
`net10.0-windows10.0.19041.0` back to the `TargetFrameworks` in the
`.csproj` if you also want a Windows build.

**Building it** needs the MAUI workload, which this sandbox couldn't
install (`dotnet workload install maui` fails here - no Android/iOS SDKs
in this Linux container, and the install itself needs the same blocked
NuGet access):
```bash
dotnet workload install maui
dotnet build src/HealingNaturalFarms.Maui -f net10.0-android
# or -f net10.0-ios / -f net10.0-maccatalyst
```

**Structure** mirrors the Blazor storefront's `Services/` folder
one-for-one, adapted to MAUI idioms rather than shared as one project
(each client's auth/guest-header plumbing can then evolve
independently):
- `Services/ApiClient.cs` - the same REST calls as the web app's.
- `Services/AuthState.cs` - the JWT session, but via `SecureStorage`
  (Keychain/Keystore-backed) instead of the web app's localStorage,
  since a mobile session typically lives far longer between launches.
- `Services/RegionState.cs`, `GuestIdProvider.cs` - via `Preferences`
  (unencrypted, appropriate for non-sensitive values).
- `Services/CartState.cs`, `OrderState.cs` - identical contracts to the
  web app's.
- `Pages/` - `ShopPage` (region/department/category/search + a product
  grid via `CollectionView`), `ProductDetailPage`, `CartPage`,
  `CheckoutPage`, `LoginPage` (doubles as the Account tab), `RegisterPage`,
  `OrderConfirmationPage`. Navigation is Shell-based: a bottom `TabBar`
  (Shop/Cart/Account) plus pushed routes for detail/checkout/confirm/
  register.
- `Resources/Styles/Colors.xaml` + `Styles.xaml` - the same green/earth
  palette as the web app's `app.css`, as MAUI styles/colors instead of
  CSS.

**Checkout's payment bridge** is the one genuinely tricky part of this
app: a plain MAUI `WebView` (via `HtmlWebViewSource`) has no built-in
JavaScript-to-C# call channel - that needs `HybridWebView`, a different,
newer control. Rather than pull in a different control (or a payment
SDK NuGet package sight-unseen), `CheckoutPage` loads
`Resources/Raw/checkout.html` - a bundled page that mirrors the web
app's `payments.js` logic (Stripe Elements / PayPal Buttons / Razorpay
Checkout, loaded from each provider's CDN at runtime) - with the
checkout details (`clientSecret`, provider order id, amount, etc.)
substituted into it via plain string replacement before it's loaded.
When that page has a payment result, it navigates to a
`app://payment-result?...` URL instead of showing it; `CheckoutPage`
intercepts that specific navigation in the WebView's `Navigating` event,
cancels it before the WebView ever tries to actually load a nonsense
URL, and parses the outcome out of the query string. This needed no new
NuGet packages, but it does mean the device needs real internet access
at checkout time to reach Stripe/PayPal/Razorpay's CDN scripts - same as
any web checkout embedded in an app.

**Push notifications** are wired up end-to-end except for the one leaf
call that needs a Firebase/APNs package this sandbox can't install and
verify: `Services/PushTokenService.cs` returns `null` in place of an
actual device token, with a long doc comment on the exact package
(Android needs a Firebase Messaging binding + `google-services.json`;
iOS needs the Push Notifications capability + a Firebase iOS binding to
exchange the raw APNs token for an FCM one) and native setup steps to
add. Everything downstream of a real token already works:
`Services/PushRegistrationService.cs` posts whatever token it gets to
`api/notifications/devices` on app startup (see `App.xaml.cs`), and the
server's `FirebasePushNotificationSender` (built in the API phase) sends
through FCM's v1 API to it. `AndroidManifest.xml` already declares the
Android 13+ `POST_NOTIFICATIONS` permission and `Info.plist`/
`Entitlements.plist` already declare the iOS remote-notification
background mode and `aps-environment` - the native plumbing is in place,
only the token-retrieval package is missing.

**Not included**: real app icon/splash artwork (plain placeholder SVGs
- a green rounded square with a simple leaf glyph - swap
`Resources/AppIcon/*.svg` and `Resources/Splash/splash.svg`) and custom
fonts (none bundled; the app uses each OS's default font rather than
referencing a `.ttf` file that doesn't exist).

## Opening in VS Code

Open the `HealingNaturalFarms` folder directly. Install the "C# Dev Kit"
extension if you don't have it, and the ".NET MAUI" extension too if
you'll be working on the mobile app. `dotnet build` from the integrated
terminal is the fastest way to confirm everything restores correctly on
your machine - run `dotnet workload install maui` first if you haven't
built a MAUI project on this machine before (see "The MAUI app" above).
