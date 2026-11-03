using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetRockTemplate.Application.Persistence;
// @init:if auth=jwt
using NetRockTemplate.Infrastructure.Features.Authentication.Extensions;
// @init:end
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

        // @init:if auth=jwt
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
        // @init:end

        private IServiceCollection ConfigureDbContext(IConfiguration configuration)
        {
            services.AddScoped<AuditingInterceptor>();
            // @init:if auth=jwt
            services.AddScoped<UserCacheInvalidationInterceptor>();
            // @init:end
            services.AddDbContext<NetRockTemplateDbContext>((sp, opt) =>
            {
                var connectionString = configuration.GetConnectionString("Database");
                opt.UseNpgsql(connectionString);
                // @init:if auth=jwt
                opt.AddInterceptors(
                    sp.GetRequiredService<AuditingInterceptor>(),
                    sp.GetRequiredService<UserCacheInvalidationInterceptor>());
                // @init:else uncomment
                // opt.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
                // @init:end
            });
            return services;
        }
    }
}
