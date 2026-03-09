using Microsoft.Extensions.DependencyInjection;
using NetRockTemplate.Application.Features.Captcha;
using NetRockTemplate.Infrastructure.Features.Captcha.Options;
using NetRockTemplate.Infrastructure.Features.Captcha.Services;

namespace NetRockTemplate.Infrastructure.Features.Captcha.Extensions;

/// <summary>
/// Extension methods for registering CAPTCHA verification services.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers Cloudflare Turnstile CAPTCHA services and configuration.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddCaptchaServices()
        {
            services.AddHttpContextAccessor();
            services.AddOptions<CaptchaOptions>()
                .BindConfiguration(CaptchaOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<ICaptchaService, TurnstileCaptchaService>();
            return services;
        }
    }
}
