using System.Text;
using HealingNaturalFarms.Api.Services;
using HealingNaturalFarms.Infrastructure.Data;
using HealingNaturalFarms.Infrastructure.Identity;
using HealingNaturalFarms.Payments.Abstractions;
using HealingNaturalFarms.Payments.Gateways;
using HealingNaturalFarms.Payments.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Database -------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ---- Identity ---------------------------------------------------------
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ---- JWT auth -----------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]!;

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ---- Payments: options + one HttpClient-backed gateway per provider ---
var paymentsOptions = builder.Configuration.GetSection(PaymentsOptions.SectionName).Get<PaymentsOptions>()
    ?? new PaymentsOptions();
builder.Services.AddSingleton(paymentsOptions);
builder.Services.AddSingleton(paymentsOptions.Stripe);
builder.Services.AddSingleton(paymentsOptions.PayPal);
builder.Services.AddSingleton(paymentsOptions.Razorpay);

builder.Services.AddHttpClient<StripeGateway>();
builder.Services.AddHttpClient<PayPalGateway>();
builder.Services.AddHttpClient<RazorpayGateway>();
builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<StripeGateway>());
builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<PayPalGateway>());
builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<RazorpayGateway>());
builder.Services.AddSingleton<IPaymentGatewayResolver, PaymentGatewayResolver>();

// ---- App services -----------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHttpClient<IPushNotificationSender, FirebasePushNotificationSender>();

// ---- Order confirmation email (SMTP - see OrderEmailSender for why no
// NuGet mail package is used) --------------------------------------------
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IOrderEmailSender, OrderEmailSender>();

// ---- CORS (Blazor Web + MAUI app) -------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Storefronts", policy =>
    {
        // MAUI's HttpClient doesn't send an Origin header, so CORS only
        // gates the Blazor WebAssembly client - the MAUI app is unaffected
        // either way since CORS is a browser-enforced mechanism.
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Storefronts");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
