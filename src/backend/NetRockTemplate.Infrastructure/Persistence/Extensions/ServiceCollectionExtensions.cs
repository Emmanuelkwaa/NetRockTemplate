using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetRockTemplate.Application.Persistence;
using NetRockTemplate.Infrastructure.Features.Authentication.Extensions;
using NetRockTemplate.Infrastructure.Persistence.Interceptors;

namespace NetRockTemplate.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for registering persistence services (DbContext and repositories).
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the database context and generic repository.
        /// </summary>
        /// <param name="configuration">The application configuration for reading connection strings.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddPersistence(IConfiguration configuration)
        {
            services.ConfigureDbContext(configuration);
            services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseEntityRepository<>));

            return services;
        }

        /// <summary>
        /// Registers ASP.NET Identity, JWT authentication, and authentication services.
        /// </summary>
        /// <param name="configuration">The application configuration for reading auth options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddIdentityServices(IConfiguration configuration)
        {
            services.AddIdentity<NetRockTemplateDbContext>(configuration);

            return services;
        }

        private IServiceCollection ConfigureDbContext(IConfiguration configuration)
        {
            services.AddScoped<AuditingInterceptor>();
            services.AddScoped<UserCacheInvalidationInterceptor>();
            services.AddDbContext<NetRockTemplateDbContext>((sp, opt) =>
            {
                var connectionString = configuration.GetConnectionString("Database");
                opt.UseNpgsql(connectionString);
                opt.AddInterceptors(
                    sp.GetRequiredService<AuditingInterceptor>(),
                    sp.GetRequiredService<UserCacheInvalidationInterceptor>());
            });
            return services;
        }
    }
}
