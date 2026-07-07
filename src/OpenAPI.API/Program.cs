using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenAPI.API.Extensions;
using OpenAPI.Domain.Entities.Auth;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Policy constants
const string CorsPolicy = "ApiCorsPolicy";
const string GlobalRateLimit = "GlobalLimit";

// ------------------------- Serilog -------------------------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog((ctx, services, configuration) =>
{
    configuration.ReadFrom.Configuration(ctx.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext();
});

// ------------------------- Services -------------------------
builder.Services.AddControllersWithViews();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CBSL Open API", Version = "v1", Description = "Central Bank Of Sri Lanka (OpenAPI + SDMX export)" });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Enter 'Bearer {token}' (without quotes).",
        Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtSecurityScheme, Array.Empty<string>() } });

    // Optional: include XML comments if you enabled <GenerateDocumentationFile>true</GenerateDocumentationFile> in csproj
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// ------------------------- Configuration objects -------------------------
// Bind JwtSettings into DI so controllers can get it via IOptions<JwtSettings>
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Bind SdmxSettings into DI so services can get it via IOptions<SdmxSettings>
builder.Services.Configure<SdmxSettings>(builder.Configuration.GetSection("Sdmx"));

// ------------------------- Rate limiting -------------------------
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(GlobalRateLimit, opts =>
    {
        opts.PermitLimit = 100;
        opts.Window = TimeSpan.FromSeconds(10);
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429;
});

// ------------------------- CORS -------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(
            "http://localhost:7091",                // dev - local
                        "https://localhost:7091",               // dev - local https
                        "https://dev.cbsl.lk",                  // dev
                        "https://qa.cbsl.lk",                   // QA
                        "https://uat.cbsl.lk",                  // UAT
                        "https://cbsl.lk")                      // production
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ------------------------- Authentication & Authorization -------------------------
// Validate presence of JWT configuration and fail fast with a helpful error
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    Log.Fatal("JWT configuration missing. Please set Jwt:Key, Jwt:Issuer, Jwt:Audience in configuration.");
    throw new InvalidOperationException("Missing required JWT configuration (Jwt:Key, Jwt:Issuer, Jwt:Audience).");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true; // enforce HTTPS for token validation
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(10)
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("common", policy => policy.RequireClaim("scope", "common"));
    options.AddPolicy("account", policy => policy.RequireClaim("scope", "account"));
    options.AddPolicy("statistics", policy => policy.RequireClaim("scope", "statistics"));

    options.AddPolicy("client1", policy => policy.RequireClaim("client_id", "client1"));
    options.AddPolicy("client2", policy => policy.RequireClaim("client_id", "client2"));
});

// ------------------------- DI: Repositories & Services -------------------------
// Register DI via extension
builder.Services.AddServiceAndRepositoryRegistration();

// Optional: health checks (useful for k8s/readiness)
builder.Services.AddHealthChecks();

var app = builder.Build();

// ------------------------- Pipeline -------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Generic exception handler for production with logging
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var exFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            Log.Error(exFeature?.Error, "Unhandled exception");

            await context.Response.WriteAsJsonAsync(new
            {
                Title = "An internal server error occurred.",
                Status = 500
            });
        });
    });

    app.UseHsts();
}

// Swagger UI available at /docs
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CBSL Hybrid API v1");
    c.RoutePrefix = "docs"; // docs at /docs
});

app.UseSwagger(options =>
{
    // Important: Use a route template that Scalar can easily find/map to.
    options.RouteTemplate = "/openapi/{documentName}.json";
});

app.MapScalarApiReference(options =>
{
    // Optional: Customize Scalar's title (it will default to OpenAPI's title if omitted)
    options.WithTitle("CBSL Open API Documentation")
           // Optional: You can customize the path where the OpenAPI spec is found
           // options.WithOpenApiRoutePattern("/openapi/{documentName}.json"); // Same as UseSwagger
           // The /openapi/v1.json URL is used by default if you use the route template above
           .AddDocument("v1"); // Add your document name
});

app.UseHttpsRedirection();

//// Serve static assets (CDN fallbacks and custom styles)
app.UseStaticFiles();


// Request logging
app.UseSerilogRequestLogging();

// Rate limiting first (protects against bursts)
app.UseRateLimiter();

// CORS before auth so pre-flight works
app.UseCors(CorsPolicy);

app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map MVC default route for Home/Index and API controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers().RequireRateLimiting(GlobalRateLimit);
app.MapHealthChecks("/health");

await app.RunAsync();



