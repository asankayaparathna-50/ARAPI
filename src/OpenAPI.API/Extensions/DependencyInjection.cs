using OpenAPI.Application.Services;
using OpenAPI.Domain.Interfaces;
using OpenAPI.Infrastructure.Repositories;
using OpenAPI.Infrastructure.Repositories.Data;
using OpenAPI.Domain.Entities.Auth;

namespace OpenAPI.API.Extensions
{
    /// <summary>
    /// Registers application services and repository implementations into the DI container.
    /// Call <c>builder.Services.AddServiceAndRepositoryRegistration()</c> in Program.cs.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddServiceAndRepositoryRegistration(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // --- Repositories ---
            services.AddScoped<ISqlQueryRepository, SqlQueryRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IDataLibraryRepository, DataLibraryRepository>();
            services.AddScoped<ICommnRepository, CommonRepository>();
            services.AddScoped<IStatisticsRepository, StatisticsRepository>();

            // --- Application Services (prefer interface registrations where available) ---
            // If you have IClientService, ICommonService etc, prefer AddScoped<IClientService, ClientServices>()
            services.AddScoped<ClientServices>();
            services.AddScoped<DataLibraryServices>();
            services.AddScoped<CommonServices>();
            services.AddScoped<StatisticsServices>();
            services.AddScoped<SdmxTransformationService>();
            services.AddScoped<EstatSdmxMappingService>();
            services.AddScoped<EuristatSdmxTransformationService>();

            return services;
        }
    }
}
