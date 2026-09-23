using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SNAP.api.Services;
using SNAP.domain.entities;
using SNAP.infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

var configuredMasterErp = builder.Configuration.GetConnectionString("MasterErp");
var masterErpConnectionString = string.IsNullOrWhiteSpace(configuredMasterErp)
    ? Environment.GetEnvironmentVariable("SNAP_MASTERERP_CONNECTIONSTRING") ?? string.Empty
    : configuredMasterErp;

var configuredTenantErp = builder.Configuration.GetConnectionString("TenantErp");
var tenantErpConnectionString = string.IsNullOrWhiteSpace(configuredTenantErp)
    ? Environment.GetEnvironmentVariable("SNAP_TENANTERP_CONNECTIONSTRING") ?? masterErpConnectionString
    : configuredTenantErp;

// 1. Register MasterErpDbContext with Transient Error Resiliency enabled
builder.Services.AddDbContext<MasterErpDbContext>(options =>
    options.UseSqlServer(
        masterErpConnectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Still registered for the design-time/`dotnet ef` tooling and for the fallback default tenant
// referenced by TenantErpConnectionString above; real per-request tenant resolution goes through
// TenantDbContextFactory instead, which builds its own TenantErpDbContext per company.
builder.Services.AddDbContext<TenantErpDbContext>(options =>
    options.UseSqlServer(
        tenantErpConnectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

if (string.IsNullOrWhiteSpace(masterErpConnectionString))
{
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantDbContextFactory>();
builder.Services.AddSingleton<JwtTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("SNAP_JWT_KEY")
    ?? throw new InvalidOperationException("Jwt:Key is not configured (set it in appsettings.json or SNAP_JWT_KEY).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SNAP.api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the token's own claim type strings ("sub", "companyId", ...) intact on the
        // validated principal instead of ASP.NET Core's default remapping to long ClaimTypes URIs -
        // TenantDbContextFactory reads claims by their original short names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // PurchaseOrderLine.PurchaseOrder is a back-reference to its own parent - IgnoreCycles
        // stops System.Text.Json from throwing on that instead of requiring every entity graph
        // returned from a controller to be hand-flattened.
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// Build application pipeline AFTER registering all services
var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Translates the business-rule/access exceptions the Controllers throw (e.g.
// TenantDbContextFactory rejecting a token with no company scope) into the same clean
// { "message": "..." } JSON shape a normal BadRequest/NotFound already returns, instead of an
// unhandled exception bubbling into a bare 500 - ApiClient (the WinForms side) expects that shape
// for every non-2xx response.
app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = exception switch
    {
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };
    var message = exception is UnauthorizedAccessException or InvalidOperationException
        ? exception.Message
        : "An unexpected error occurred.";
    await context.Response.WriteAsJsonAsync(new { message });
}));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapPost("/companies", async (Company company, MasterErpDbContext db) =>
{
    db.Companies.Add(company);
    await db.SaveChangesAsync();
    return Results.Created($"/companies/{company.CompanyId}", company);
});

app.Run();
