using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Core.Extensions
{
    /// <summary>
    /// Extension methods to configure JWT authentication with Azure AD B2C
    /// </summary>
    public static class JwtAuthenticationExtensions
    {

        /// <summary>
        /// Gets the preferred language from the request headers
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>The language code (en or ar)</returns>
        private static string GetPreferredLanguage(HttpContext context)
        {
            // Check for the X-UserLanguage header
            if (context.Request.Headers.TryGetValue("X-UserLanguage", out var languageHeader))
            {
                string language = languageHeader.ToString().ToLowerInvariant();

                // Only support "en" or "ar" languages
                if (language == "ar")
                {
                    return "ar";
                }
            }

            // Default to English
            return "en";
        }

        /// <summary>
        /// Adds and configures the JWT Bearer authentication for Azure AD B2C
        /// </summary>
        /// <param name="services">The service collection to add authentication to</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The authentication builder for further customization</returns>
        public static AuthenticationBuilder AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // ✅ DIRECT CONFIGURATION ACCESS TO BYPASS COMPUTED PROPERTIES
            var issuer = configuration["Authentication:Adb2c:Issuer"];
            var audience = configuration["Authentication:Adb2c:Audience"];
            var openIdConfigUrl = configuration["Authentication:Adb2c:OpenIdConfigurationUrl"];

            // Bind the ADB2C settings from the configuration
            var adb2cSettings = new JwtAuthenticationSettings();
            configuration.GetSection("Authentication:Adb2c").Bind(adb2cSettings);

            // Register the settings as a singleton for dependency injection
            services.AddSingleton(adb2cSettings);

            // Configure the JWT Bearer authentication
            return services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Set token validation parameters
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Validates the security key that was used to sign the JWT
                    ValidateIssuerSigningKey = true,

                    // Validates the issuer to prevent token forgery
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    // Validates the audience to ensure the token is intended for this API
                    ValidateAudience = false,
                    ValidAudience = audience,

                    // Validates token lifetime to ensure it's not expired
                    ValidateLifetime = true,

                    // Set clock skew to zero for stricter validation (adjusts for time drift between servers)
                    ClockSkew = TimeSpan.Zero,

                    // Require signed tokens
                    RequireSignedTokens = true,

                    // Enable metadata-based configuration for key discovery
                    RequireExpirationTime = true,

                    LogValidationExceptions = true

                };

                // Configure the OpenID Connect configuration manager for key discovery
                // Use the OpenIdConfigurationUrl property that includes the policy parameter
                var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    openIdConfigUrl,
                    new OpenIdConnectConfigurationRetriever());

                options.ConfigurationManager = configManager;

                // Handle authentication failed events
                options.Events = new JwtBearerEvents
                {
                    // Add this to skip validation for non-authorized endpoints
                    OnMessageReceived = context =>
                    {
                        var endpoint = context.HttpContext.GetEndpoint();
                        if (endpoint == null)
                        {
                            return Task.CompletedTask;
                        }

                        // Check for AllowAnonymous at endpoint level first
                        var allowAnonymous = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;

                        if (!allowAnonymous)
                        {
                            // Check controller action for more detailed analysis
                            var controllerActionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                            if (controllerActionDescriptor != null)
                            {
                                // Method-level AllowAnonymous takes precedence
                                var methodHasAllowAnonymous = controllerActionDescriptor.MethodInfo
                                    .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
                                    .Any();

                                var methodHasAuthorize = controllerActionDescriptor.MethodInfo
                                    .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                                    .Any();

                                // Controller-level attributes
                                var controllerHasAllowAnonymous = controllerActionDescriptor.ControllerTypeInfo
                                    .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
                                    .Any();

                                // Determine if anonymous access is allowed
                                if (methodHasAllowAnonymous || (!methodHasAuthorize && controllerHasAllowAnonymous))
                                {
                                    allowAnonymous = true;
                                }
                            }
                        }

                        // Skip JWT validation for anonymous endpoints
                        if (allowAnonymous)
                        {
                            context.NoResult();
                        }

                        return Task.CompletedTask;
                    },

                    // This event is fired when an incoming request has an invalid token

                    OnAuthenticationFailed = context =>
                    {
                        // Add detailed logging for debugging
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                        logger.LogError("=== Token Validation FAILED ===");
                        logger.LogError("Exception Type: {ExceptionType}", context.Exception.GetType().Name);
                        logger.LogError("Exception Message: {ExceptionMessage}", context.Exception.Message);

                        // ✅ LOG INNER EXCEPTIONS FOR MORE DETAILS
                        if (context.Exception.InnerException != null)
                        {
                            logger.LogError("Inner Exception: {InnerException}", context.Exception.InnerException.Message);
                        }

                        // ✅ LOG FULL STACK TRACE FOR DEBUGGING
                        logger.LogError("Full Exception: {FullException}", context.Exception.ToString());

                        logger.LogError("================================");

                        // Get the preferred language from the request headers
                        string language = GetPreferredLanguage(context.HttpContext);

                        logger.LogInformation("Using language: {Language} for error message", language);

                        // Create different error messages based on the type of exception
                        string messageKey = context.Exception switch
                        {
                            SecurityTokenExpiredException => "TokenExpired",
                            SecurityTokenInvalidAudienceException => "InvalidAudience",
                            SecurityTokenInvalidIssuerException => "InvalidIssuer",
                            SecurityTokenSignatureKeyNotFoundException => "SignatureKeyNotFound",
                            SecurityTokenValidationException => "TokenValidationFailed",
                            SecurityTokenException => "SecurityTokenError",
                            _ => "GenericAuthError"
                        };

                        logger.LogInformation("Selected messageKey: {MessageKey} for exception type: {ExceptionType}",
                            messageKey, context.Exception.GetType().Name);

                        string errorMessage;
                        try
                        {
                            // Get the localized error message
                            errorMessage ="Localized message retrieved: {ErrorMessage}";
                            logger.LogInformation("Localized message retrieved: {ErrorMessage}", errorMessage);

                        }
                        catch (Exception localizationEx)
                        {
                            logger.LogError(localizationEx, "Error occurred while getting localized message for key: {MessageKey}", messageKey);
                            errorMessage = localizationEx.Message;
                        }

                        // Set status code to 401
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

                        // Store the error message in HttpContext.Items
                        context.HttpContext.Items["AuthError"] = errorMessage;

                        logger.LogInformation("Final error message set: {ErrorMessage}", errorMessage);

                        // Don't write the response here - THIS IS IMPORTANT
                        return Task.CompletedTask;
                    },

                    // This event is fired when a token has been validated and a ClaimsIdentity has been created
                    OnTokenValidated = async context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerHandler>>();

                        logger.LogInformation("=== Token Validation SUCCESS ===");
                        logger.LogInformation("Token Issuer: {TokenIssuer}", context.Principal.FindFirst("iss")?.Value);
                        logger.LogInformation("Token Audience: {TokenAudience}", context.Principal.FindFirst("aud")?.Value);
                        logger.LogInformation("Token Subject: {TokenSubject}", context.Principal.FindFirst("sub")?.Value);
                        logger.LogInformation("Token Expiry: {TokenExpiry}", context.Principal.FindFirst("exp")?.Value);
                        logger.LogInformation("================================");

                        // Get the preferred language from the request headers
                        string language = GetPreferredLanguage(context.HttpContext);

                        // Extract the user ID from the claims
                        var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
                        var objectIdClaim = claimsIdentity?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")
                                          ?? claimsIdentity?.FindFirst("oid");

                        if (objectIdClaim == null)
                        {
                            logger.LogWarning("User ID claim not found in token");
                            string errorMessage = "User ID claim not found in token";
                            context.Fail(errorMessage);
                            return;
                        }

                        // Here you would typically lookup the user in your database
                        // And verify they have permission to access the requested resource

                        // Optionally add custom claims based on your database authorization
                        // claimsIdentity.AddClaim(new Claim("custom_claim", "value"));
                        logger.LogInformation("User ID extracted: {UserId}", objectIdClaim.Value);

                        await Task.CompletedTask.ConfigureAwait(false);
                    },

                    // This event is fired when a request without a token tries to access a protected resource
                    OnChallenge = context =>
                    {
                        // Get the preferred language from the request headers
                        string language = GetPreferredLanguage(context.HttpContext);

                        // Prevent default response
                        context.HandleResponse();

                        // Set status code to 401
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

                        // Only set the error message if one doesn't already exist
                        if (!context.HttpContext.Items.ContainsKey("AuthError"))
                        {
                            // Get localized error message
                            string errorMessage = "";
                            context.HttpContext.Items["AuthError"] = errorMessage;
                        }

                        // Don't write to the response - THIS IS IMPORTANT
                        return Task.CompletedTask;
                    },

                    // This event is fired when the required scope is not present in the token
                    OnForbidden = context =>
                    {
                        // Get the preferred language from the request headers
                        string language = GetPreferredLanguage(context.HttpContext);

                        // Set status code to 403
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

                        // Get localized error message
                        string errorMessage = "";

                        // Store the error message in HttpContext.Items
                        context.HttpContext.Items["AuthError"] = errorMessage;

                        // Don't write to the response - THIS IS IMPORTANT
                        return Task.CompletedTask;
                    }
                };
            });
        }
    }
    public class JwtAuthenticationSettings
    {
        public string TenantId { get; set; }
        public string AppId { get; set; }
        public string ClientSecret { get; set; }
        public string B2cExtensionAppClientId { get; set; }
        public string UsersFileName { get; set; }
        public string FrontEndWebAddress { get; set; }
        public string TokenAuthority { get; set; }
        public string Scopes { get; set; }

        // Default policy if none is provided in the configuration
        private const string DEFAULT_POLICY = "B2C_1_event_flow_signin";

        /// <summary>
        /// Extracts the policy name from the TokenAuthority if available
        /// </summary>
        public string PolicyName
        {
            get
            {
                if (string.IsNullOrEmpty(TokenAuthority))
                {
                    return DEFAULT_POLICY;
                }

                try
                {
                    // Try to extract policy from the TokenAuthority
                    var uri = new Uri(TokenAuthority, UriKind.Absolute);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var policy = query["p"];

                    return !string.IsNullOrEmpty(policy) ? policy : DEFAULT_POLICY;
                }
                catch
                {
                    // If there's any error parsing the URL, return the default policy
                    return DEFAULT_POLICY;
                }
            }
        }

        // Computed property to generate the issuer based on the tenant ID
        public string Issuer => $"https://{TenantId.Split('.')[0]}.b2clogin.com/{TenantId}/v2.0/";

        // Computed property to generate the audience from the app ID
        public string Audience => AppId;

        /// <summary>
        /// Gets the properly formatted OpenID configuration URL with policy parameter
        /// </summary>
        public string OpenIdConfigurationUrl => $"{Issuer}.well-known/openid-configuration?p={PolicyName}";

    }
}