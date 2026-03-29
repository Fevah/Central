using Elsa.Extensions;
using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Management;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Central.Workflows;

/// <summary>Assembly marker for Elsa activity/workflow scanning.</summary>
public class WorkflowsAssemblyMarker;

/// <summary>
/// Shared Elsa Workflows registration for Central platform.
/// Call AddCentralWorkflows() from any host (API server, desktop background service).
/// </summary>
public static class WorkflowSetup
{
    /// <summary>
    /// Register Elsa Workflows engine with PostgreSQL persistence and all Central custom activities.
    /// </summary>
    public static IServiceCollection AddCentralWorkflows(this IServiceCollection services, string connectionString)
    {
        services.AddElsa(elsa =>
        {
            // ── Persistence: PostgreSQL via EF Core ──
            elsa.UseWorkflowManagement(management =>
                management.UseEntityFrameworkCore(ef =>
                    ef.UsePostgreSql(connectionString)));

            elsa.UseWorkflowRuntime(runtime =>
                runtime.UseEntityFrameworkCore(ef =>
                    ef.UsePostgreSql(connectionString)));

            // ── Identity (API auth) ──
            elsa.UseIdentity(identity =>
            {
                identity.UseAdminUserProvider();
            });

            // ── HTTP activities ──
            elsa.UseHttp();

            // ── Scheduling (timers, cron) ──
            elsa.UseScheduling();

            // ── C# expressions ──
            elsa.UseCSharp();

            // ── REST API for workflow management ──
            elsa.UseWorkflowsApi();

            // ── Register all Central custom activities from this assembly ──
            elsa.AddActivitiesFrom<WorkflowsAssemblyMarker>();

            // ── Register built-in workflow definitions from this assembly ──
            elsa.AddWorkflowsFrom<WorkflowsAssemblyMarker>();
        });

        return services;
    }
}
