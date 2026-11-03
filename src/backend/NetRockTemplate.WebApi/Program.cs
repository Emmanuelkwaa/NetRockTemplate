using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
// @init:if auth=jwt
using Microsoft.AspNetCore.Authorization;
// @init:end
using NetRockTemplate.Infrastructure.Caching.Extensions;
// @init:if auth=jwt,keycloak
using NetRockTemplate.Infrastructure.Cookies.Extensions;
// @init:end
// @init:if auth=jwt
using NetRockTemplate.Infrastructure.Features.Admin.Extensions;
using NetRockTemplate.Infrastructure.Features.Audit.Extensions;
using NetRockTemplate.Infrastructure.Features.Avatar.Extensions;
using NetRockTemplate.Infrastructure.Features.Captcha.Extensions;
// @init:end
// @init:if auth=keycloak uncomment
// using NetRockTemplate.Infrastructure.Features.Authentication.Extensions;
// @init:end
using NetRockTemplate.Infrastructure.Features.FileStorage.Extensions;
using NetRockTemplate.Infrastructure.Features.Email.Extensions;
using NetRockTemplate.Infrastructure.Features.Jobs.Extensions;
// @init:if auth=jwt,keycloak
using NetRockTemplate.Infrastructure.Identity.Extensions;
// @init:end
using NetRockTemplate.Infrastructure.Persistence.Extensions;
// @init:if auth=jwt
using NetRockTemplate.WebApi.Authorization;
// @init:end
using NetRockTemplate.WebApi.Extensions;
using NetRockTemplate.WebApi.Features.OpenApi.Extensions;
using NetRockTemplate.WebApi.Middlewares;
// @init:if auth=jwt
using NetRockTemplate.WebApi.Routing;
// @init:end
using Serilog;
using LoggerConfigurationExtensions = NetRockTemplate.Infrastructure.Logging.Extensions.LoggerConfigurationExtensions;

var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Production;

Log.Logger = LoggerConfigurationExtensions.ConfigureMinimalLogging(environmentName);

try
{
    Log.Information("Starting web host");
    var builder = WebApplication.CreateBuilder(args);

    Log.Debug("Use Serilog");
    builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    {
        LoggerConfigurationExtensions.SetupLogger(context.Configuration, loggerConfiguration);
    }, preserveStaticLogger: true, writeToProviders: true);

    Log.Debug("Adding Aspire service defaults (OpenTelemetry, service discovery, resilience)");
    builder.AddServiceDefaults();

    try
    {
        Log.Debug("Adding TimeProvider");
        builder.Services.AddSingleton(TimeProvider.System);

        Log.Debug("Adding persistence services");
        builder.Services.AddPersistence(builder.Configuration);

        // @init:if auth=jwt
        Log.Debug("Adding identity services");
        builder.Services.AddIdentityServices(builder.Configuration);

        // @init:end
        // @init:if auth=keycloak uncomment
        // Log.Debug("Adding Keycloak authentication");
        // builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
        //
        // @init:end
        // @init:if auth=jwt,keycloak
        Log.Debug("Adding user context");
        builder.Services.AddUserContext();

        // @init:end
        Log.Debug("Adding caching");
        builder.Services.AddCaching(builder.Configuration);

        // @init:if auth=jwt,keycloak
        Log.Debug("Adding cookie services");
        builder.Services.AddCookieServices();

        // @init:end
        // @init:if auth=jwt
        Log.Debug("Adding admin services");
        builder.Services.AddAdminServices();

        Log.Debug("Adding audit services");
        builder.Services.AddAuditServices();

        // @init:end
        Log.Debug("Adding email services");
        builder.Services.AddEmailServices(builder.Configuration);

        Log.Debug("Adding file storage services");
        builder.Services.AddFileStorageServices(builder.Configuration);

        // @init:if auth=jwt
        Log.Debug("Adding avatar services");
        builder.Services.AddAvatarServices();

        Log.Debug("Adding captcha services");
        builder.Services.AddCaptchaServices();

        // @init:end
        Log.Debug("Adding job scheduling");
        builder.Services.AddJobScheduling(builder.Configuration);
        // @init:if auth=jwt

        Log.Debug("Adding permission-based authorization");
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationHandler>();
        // @init:end
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to configure essential services or dependencies.");
        throw;
    }

    Log.Debug("Adding Cors Feature");
    builder.Services.AddCors(builder.Configuration, builder.Environment);

    // @init:if auth=jwt
    Log.Debug("Adding Routing => LowercaseUrls, Custom Constraints");
    builder.Services.AddRouting(options =>
    {
        options.LowercaseUrls = true;
        options.ConstraintMap.Add("roleName", typeof(RoleNameRouteConstraint));
        options.ConstraintMap.Add("jobId", typeof(JobIdRouteConstraint));
        options.ConstraintMap.Add("providerName", typeof(ProviderNameRouteConstraint));
    });
    // @init:else uncomment
    // Log.Debug("Adding Routing => LowercaseUrls");
    // builder.Services.AddRouting(options => options.LowercaseUrls = true);
    // @init:end

    Log.Debug("Adding ProblemDetails");
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        };
    });

    Log.Debug("Adding Controllers");
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    Log.Debug("Adding FluentValidation");
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    Log.Debug("Adding hosting options");
    builder.Services.AddHostingOptions(builder.Configuration);

    Log.Debug("Adding rate limiting");
    builder.Services.AddRateLimiting(builder.Configuration);

    Log.Debug("ConfigureServices => Setting AddHealthChecks");
    builder.Services.AddApplicationHealthChecks(builder.Configuration);

    Log.Debug("ConfigureServices => Setting AddApiDefinition");
    builder.AddOpenApiSpecification();

    var app = builder.Build();

    Log.Debug("Setting hosting middleware (forwarded headers, HTTPS)");
    app.UseHostingMiddleware();

    if (!app.Environment.IsProduction())
    {
        Log.Debug("Setting Scalar OpenApi Documentation");
        app.UseOpenApiDocumentation();
    }

    Log.Debug("Setting security headers");
    app.UseSecurityHeaders();

    if (!app.Environment.IsDevelopment())
    {
        Log.Debug("Setting HSTS");
        app.UseHsts();
    }

    Log.Debug("Initializing database");
    await app.InitializeDatabaseAsync();

    Log.Debug("Setting UseCors");
    CorsExtensions.UseCors(app);

    Log.Debug("Setting UseMiddleware => OriginValidationMiddleware");
    app.UseMiddleware<OriginValidationMiddleware>();

    Log.Debug("Setting UseSerilogRequestLogging");
    app.UseSerilogRequestLogging();

    Log.Debug("Setting UseMiddleware => ExceptionHandlingMiddleware");
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    Log.Debug("Setting UseStatusCodePages");
    app.UseStatusCodePages();

    if (!app.Environment.IsDevelopment())
    {
        Log.Debug("Setting UseHttpsRedirection");
        app.UseHttpsRedirection();
    }

    Log.Debug("Setting UseRouting");
    app.UseRouting();

    // @init:if auth=jwt,keycloak
    Log.Debug("Setting UseAuthentication");
    app.UseAuthentication();

    // @init:end
    Log.Debug("Setting UseRateLimiter");
    app.UseRateLimiter();
    // @init:if auth=jwt,keycloak

    Log.Debug("Setting UseAuthorization");
    app.UseAuthorization();
    // @init:end

    Log.Debug("Setting up job scheduling");
    app.UseJobScheduling();

    // @init:if frontend=yes
    Log.Debug("Setting \"security\" measure => Redirect to YouTube video to confuse enemies");
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Value is "/")
        {
            context.Response.Redirect("https://www.youtube.com/watch?v=dQw4w9WgXcQ", permanent: false);
            return;
        }

        await next();
    });
    // @init:else uncomment
    // if (!app.Environment.IsProduction())
    // {
    //     Log.Debug("Setting root redirect => API documentation");
    //     app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
    // }
    // @init:end

    Log.Debug("Setting endpoints => MapControllers");
    app.MapControllers();

    Log.Debug("Setting endpoints => MapHealthChecks");
    app.MapHealthCheckEndpoints();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down application");
    await Log.CloseAndFlushAsync();
}
