using Microsoft.Extensions.DependencyInjection;
using NetRockTemplate.Application.Identity;

namespace NetRockTemplate.Infrastructure.Identity.Extensions;

/// <summary>
/// Extension methods for registering the user context.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IUserContext"/> backed by the current HTTP request's claims.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddUserContext()
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUserContext, UserContext>();
            return services;
        }
    }
}
