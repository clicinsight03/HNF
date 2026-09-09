using HealingNaturalFarms.Web.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// When running as WebAssembly the HttpClient must point at the API's
// actual origin (the browser, not a server, is making the call) - CORS
// on the API (see Program.cs there) is what allows this cross-origin
// call to succeed. Configure the real URL in wwwroot/appsettings.json
// per environment; this falls back to localhost for local dev.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5443/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddScoped<GuestIdProvider>();
builder.Services.AddScoped<RegionState>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<OrderState>();

await builder.Build().RunAsync();
