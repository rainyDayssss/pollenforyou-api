using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PollenForYouApi.Data;

namespace PollenForYouApi.HealthChecks;

/// <summary>
/// Liveness/readiness probe for the /health endpoint: verifies the SQL
/// database is reachable by opening a connection. Dependency-free — uses
/// the already-registered <see cref="PfyDbContext"/> (no extra packages,
/// consistent with the project's minimal-dependency convention).
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly PfyDbContext _db;

    public DatabaseHealthCheck(PfyDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
