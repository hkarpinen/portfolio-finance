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
    var jwtSecret = jwtSection["Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret must be configured.");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
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

    builder.Services.AddCors(options =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins)
                  .WithHeaders("Content-Type", "Authorization")
                  .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                  .AllowCredentials());
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter("api", opt =>
        {
            opt.PermitLimit = 120;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });

        options.AddFixedWindowLimiter("write", opt =>
        {
            opt.PermitLimit = 30;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // RFC 7807 problem details.
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
        c.SwaggerDoc("v1", new() { Title = "Bills API", Version = "v1" });
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

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    await app.Services.ApplyMigrationsAsync();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Bills API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
