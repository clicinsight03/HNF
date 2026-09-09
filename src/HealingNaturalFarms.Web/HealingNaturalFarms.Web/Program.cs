using HealingNaturalFarms.Web.Client.Pages;
using HealingNaturalFarms.Web.Client.Services;
using HealingNaturalFarms.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Registered here too (in addition to Web.Client's Program.cs) because
// Interactive Auto render mode runs components on the server first
// (including the initial static prerender) before the WASM bundle
// downloads and takes over in-browser - each hosting model has its own
// DI container. When running server-side, the API is just another
// process on the same machine/network, so no CORS is involved here;
// CORS only matters for the WASM host's in-browser calls.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7156/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<GuestIdProvider>();
builder.Services.AddScoped<RegionState>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<OrderState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(HealingNaturalFarms.Web.Client._Imports).Assembly);

app.Run();
