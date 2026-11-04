---
disable-model-invocation: true
---

Adds a rate limit policy.

## Steps

1. Add a constant to `src/backend/NetRockTemplate.WebApi/Shared/RateLimitPolicies.cs`:
   ```csharp
   public const string MyPolicy = "my-policy";
   ```
2. Add a nested configuration class inside `RateLimitingOptions` in `src/backend/NetRockTemplate.WebApi/Options/RateLimitingOptions.cs` (extend `FixedWindowPolicyOptions`):
   ```csharp
   public sealed class MyPolicyLimitOptions : FixedWindowPolicyOptions
   {
       public MyPolicyLimitOptions()
       {
           PermitLimit = 10;
           Window = TimeSpan.FromMinutes(1);
           QueueLimit = 0;
       }
   }
   ```
3. Add the property to `RateLimitingOptions` (same file):
   ```csharp
   [Required]
   [ValidateObjectMembers]
   public MyPolicyLimitOptions MyPolicy { get; init; } = new();
   ```
<!-- @init:if auth=jwt -->
4. Register in `src/backend/NetRockTemplate.WebApi/Extensions/RateLimiterExtensions.cs` using existing helpers:
   - `AddIpPolicy(...)` for unauthenticated endpoints (partitions by IP)
   - `AddUserPolicy(...)` for authenticated endpoints (partitions by user identity)
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
4. Register in `src/backend/NetRockTemplate.WebApi/Extensions/RateLimiterExtensions.cs` with `AddIpPolicy(...)` (partitions by IP). For a per-user policy, add a helper that partitions by `context.User.Identity?.Name` with an IP fallback
<!-- @init:end -->
<!-- @init:if auth=none -->
4. Register in `src/backend/NetRockTemplate.WebApi/Extensions/RateLimiterExtensions.cs`: add an `options.AddPolicy(...)` that partitions by `context.Connection.RemoteIpAddress` (there are no per-policy helpers yet, and no users to partition by)
<!-- @init:end -->
5. Add config section to both `appsettings.json` and `appsettings.Development.json`
6. Apply to endpoints: `[EnableRateLimiting(RateLimitPolicies.MyPolicy)]`
7. Add `[ProducesResponseType(StatusCodes.Status429TooManyRequests)]` to the endpoint
8. Verify: `dotnet build src/backend/NetRockTemplate.slnx`
