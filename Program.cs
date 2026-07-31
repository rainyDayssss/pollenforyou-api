using System.Text;
using System.Collections.Generic;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PollenForYouApi.Data;
using PollenForYouApi.Entities;
using PollenForYouApi.Filters;
using PollenForYouApi.HealthChecks;
using PollenForYouApi.Middleware;
using PollenForYouApi.Options;
using PollenForYouApi.Profiles;
using PollenForYouApi.Repositories;
using PollenForYouApi.Services;
using PollenForYouApi.Validators;

const string CorsPolicyName = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PollenForYou API", Version = "v1" });

    // Bearer JWT definition: Swagger UI shows an "Authorize" button where you
    // paste the access token; requests then send "Authorization: Bearer {token}".
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste your JWT access token below (no \"Bearer \" prefix needed).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Apply it globally. Swashbuckle 10 / Microsoft.OpenApi 2.x: this overload
    // receives the generated document so references resolve by Id.
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            new List<string>()
        }
    });
});

builder.Services.AddDbContext<PfyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS (SRS frontend contract): the React client runs on a separate origin
// (Vercel in production, Vite dev server locally). Origins come from config so
// no redeploy is needed when the frontend URL changes.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (corsOrigins.Length == 0)
        {
            // No origins configured — deny cross-origin (secure default).
            return;
        }

        policy.WithOrigins(corsOrigins);
    });
});

// Health checks: /health liveness probe (DB connectivity). Built into the
// ASP.NET Core shared framework — no packages. Kept dependency-free via a
// custom check using the existing PfyDbContext (AGENT.md minimal-dependency rule).
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<PfyDbContext>();

var jwt = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<JwtOptions>(jwt);

// Rate limiting (SRS §4): fixed-window per-IP policy guarding the public
// checkout endpoint. Built into the ASP.NET Core shared framework — no packages.
var rateLimiting = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>()
    ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Uniform RFC 7807 body + Retry-After on rejection (AGENT.md §12).
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests",
            detail: "Too many checkout attempts. Please wait and try again.")
            .ExecuteAsync(context.HttpContext);
    };

    options.AddPolicy("checkout", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiting.CheckoutPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimiting.CheckoutWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(UserMappingProfile).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(CreateUserRequestValidator).Assembly);
builder.Services.AddScoped<ValidationFilter>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

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
