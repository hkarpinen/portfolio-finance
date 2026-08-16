using Infrastructure.Persistence;
using System.Text;
using System.Threading.RateLimiting;
using Finance.Application;
using Finance.Domain;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddDomain();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var jwtSection = builder.Configuration.GetSection("Jwt");
    var authority = jwtSection["Authority"]
        ?? throw new InvalidOperationException("Jwt:Authority must be configured — identity's base URL.");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // No key material here, deliberately. This service used to hold the HMAC secret that
            // signed tokens, which meant it could mint one for any user with any role. It now
            // fetches identity's public key set from {Authority}/.well-known/openid-configuration
            // and caches it, so rotating the key is something identity does on its own.
            options.Authority = authority;
            options.RequireHttpsMetadata = false;   // container-to-container traffic is plain HTTP

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"]
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies["access_token"]
                        ?? context.Request.Headers.Authorization
                            .FirstOrDefault()?.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Limits are configuration, not constants. The defaults below are the production posture;
        // a parallel e2e run drives far more traffic per minute than any real user and would
        // otherwise be rejected, which surfaces as the frontend's error boundary rather than as
        // anything resembling a rate-limit message. Override per environment with
        // RateLimiting__<policy>, e.g. RateLimiting__standard=2000.
        options.AddFixedWindowLimiter("api", opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:api") ?? 120;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });

        options.AddFixedWindowLimiter("write", opt =>
        {
            opt.PermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:write") ?? 30;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Instance ??= $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        };
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Finance API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new()
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header
        });
        c.AddSecurityRequirement(new()
        {
            {
                new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
                []
            }
        });
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<Infrastructure.Persistence.FinanceDbContext>();

    var app = builder.Build();

    // The custom status-code mapping below survives only because the status is set BEFORE re-throwing
    // into the ProblemDetails middleware.
    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            var exception = feature?.Error;

            Log.Error(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = exception switch
            {
                ArgumentException or ArgumentNullException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                // The Expense.Version token exists to catch exactly this: two people re-cutting one
                // expense at once. It is the caller's to retry, not a fault — 409, never 500.
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                InvalidOperationException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            var problemDetailsService = context.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new Microsoft.AspNetCore.Http.ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = context.Response.StatusCode,
                    Title = "An error occurred while processing the request.",
                    Detail = exception?.Message
                }
            });
        });
    });
    app.UseStatusCodePages();

    app.UseSerilogRequestLogging();
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    await app.Services.ApplyMigrationsAsync();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Finance API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
