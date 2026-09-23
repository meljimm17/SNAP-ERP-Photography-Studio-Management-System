using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Build application pipeline AFTER registering all services
var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapPost("/companies", async (Company company, MasterErpDbContext db) =>
{
    db.Companies.Add(company);
    await db.SaveChangesAsync();
    return Results.Created($"/companies/{company.CompanyId}", company);
});

app.Run();