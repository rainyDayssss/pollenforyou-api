using PollenForYouApi.Data;
using PollenForYouApi.Extensions;
using PollenForYouApi.Filters;
using PollenForYouApi.HealthChecks;
using PollenForYouApi.Middleware;

const string CorsPolicyName = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Custom Service Extensions (Uncluttering)
builder.Services.AddCustomSwagger();
builder.Services.AddCustomDatabase(builder.Configuration);
builder.Services.AddCustomCors(builder.Configuration, CorsPolicyName);
builder.Services.AddCustomIdentityAndAuth(builder.Configuration);
builder.Services.AddCustomRateLimiting(builder.Configuration);
builder.Services.AddApplicationDependencies();

// Health checks, problem details, exception handler
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Seed the default superadmin account and roles (idempotent bootstrap, not a worker).
await DbInitializer.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
// Request logging outermost — observes final status even for handled exceptions.
app.UseMiddleware<RequestLoggingMiddleware>();

// Centralized exception handling next — catches everything downstream.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS before auth — preflight (OPTIONS) requests must pass without a token.
app.UseCors(CorsPolicyName);

// Rate limiting before auth/endpoints so the checkout policy applies per-IP.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness/readiness probe for App Service / load balancers (anonymous).
app.MapHealthChecks("/health");

app.Run();