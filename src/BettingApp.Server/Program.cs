using BettingApp.Application;
using BettingApp.Application.Bets.Commands;
using BettingApp.Application.Bets.Queries;
using BettingApp.Infrastructure;
using BettingApp.Infrastructure.Persistence;
using BettingApp.Infrastructure.Realtime;
using BettingApp.Server.Configuration;
using BettingApp.Server.Data;
using BettingApp.Server.Models;
using BettingApp.Server.Services;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.Configure<KioskOAuthClientOptions>(builder.Configuration.GetSection(KioskOAuthClientOptions.SectionName));
builder.Services.Configure<OperatorOAuthClientOptions>(builder.Configuration.GetSection(OperatorOAuthClientOptions.SectionName));
builder.Services.Configure<BootstrapIdentityOptions>(builder.Configuration.GetSection(BootstrapIdentityOptions.SectionName));
builder.Services.Configure<ServerDiscoveryOptions>(builder.Configuration.GetSection(ServerDiscoveryOptions.SectionName));

var bettingDatabasePath = ServerStoragePaths.GetBettingDatabasePath();
var authDatabasePath = ServerStoragePaths.GetAuthDatabasePath();

Directory.CreateDirectory(Path.GetDirectoryName(bettingDatabasePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(authDatabasePath)!);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(bettingDatabasePath);

builder.Services.AddDbContext<ServerIdentityDbContext>(options =>
{
    options.UseSqlite($"Data Source={authDatabasePath}");
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ServerIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ServerIdentityDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();
        options.AllowRefreshTokenFlow();
        options.AllowClientCredentialsFlow();
        options.RegisterScopes(Scopes.DisplayRead, Scopes.Operations, Scopes.OfflineAccess, Scopes.OpenId, Scopes.Profile, Scopes.Roles);
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            // D3Bet runs on a local network where TLS is handled at the
            // infrastructure level.  Disable the OpenIddict transport check
            // so that HTTP endpoints work regardless of the hosting
            // environment (prevents ID2083 errors).
            .DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.DisplayRead, policy =>
    {
        policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope(Scopes.DisplayRead));
    });
    options.AddPolicy(Policies.Operations, policy =>
    {
        policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope(Scopes.Operations));
    });
    options.AddPolicy(Policies.AdminOnly, policy =>
    {
        policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.HasScope(Scopes.Operations));
        policy.RequireRole(Roles.Admin);
    });
});

builder.Services.AddScoped<CustomerDisplayQueryService>();
builder.Services.AddScoped<OperatorPrincipalFactory>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddSingleton<ServerAppSettingsStore>();
builder.Services.AddHostedService<ServerBootstrapHostedService>();
builder.Services.AddHostedService<ServerDiscoveryHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    const string CorrelationHeaderName = "X-Correlation-ID";
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("D3Bet.Requests");
    var correlationId = ResolveCorrelationId(context, CorrelationHeaderName);
    context.TraceIdentifier = correlationId;
    context.Response.Headers[CorrelationHeaderName] = correlationId;

    using (logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = correlationId,
        ["Path"] = context.Request.Path.Value
    }))
    {
        logger.LogInformation(
            "Zpracování požadavku {Method} {Path} bylo zahájeno.",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await next();

            logger.LogInformation(
                "Požadavek {Method} {Path} byl dokončen se stavem {StatusCode}.",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Požadavek {Method} {Path} selhal před odesláním odpovědi.",
                context.Request.Method,
                context.Request.Path);
            throw;
        }
    }
});

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (statusCode, title, detail) = exception switch
        {
            InvalidOperationException invalidOperationException => (
                StatusCodes.Status400BadRequest,
                "Požadavek se nepodařilo zpracovat",
                invalidOperationException.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server D3Bet narazil na chybu",
                "Požadovanou akci se nepodařilo dokončit. Zkuste to prosím znovu za okamžik.")
        };

        if (!ShouldWriteApiProblem(context))
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(detail);
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("D3Bet.Exceptions");
        logger.LogError(
            exception,
            "API požadavek skončil chybou {StatusCode} pro {Path}. TraceId {TraceId}.",
            statusCode,
            context.Request.Path,
            context.TraceIdentifier);

        await WriteProblemAsync(context, statusCode, title, detail);
    });
});

app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    if (!ShouldWriteApiProblem(context) || context.Response.HasStarted)
    {
        return;
    }

    var (title, detail) = context.Response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => (
            "Přihlášení je potřeba obnovit",
            "Vaše relace už není platná nebo chybí přístupový token."),
        StatusCodes.Status403Forbidden => (
            "Akce není povolená",
            "Přihlášený účet nemá oprávnění pro tuto operaci."),
        StatusCodes.Status404NotFound => (
            "Požadovaný zdroj nebyl nalezen",
            "Server nenašel požadovaný endpoint nebo záznam."),
        _ => (
            "Požadavek nebyl úspěšný",
            "Server nedokázal požadavek dokončit v očekávaném režimu.")
    };

    await WriteProblemAsync(context, context.Response.StatusCode, title, detail);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/account/login", ([FromQuery] string? returnUrl, [FromQuery] string? error) =>
{
    var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    var errorMarkup = string.IsNullOrWhiteSpace(error)
        ? string.Empty
        : $"<div style=\"margin-bottom:16px;padding:12px 14px;border-radius:12px;background:#3f1d1d;color:#fecaca;border:1px solid #7f1d1d;\">{System.Net.WebUtility.HtmlEncode(error)}</div>";

    var html = $$"""
        <!DOCTYPE html>
        <html lang="cs">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>D3Bet Přihlášení</title>
        </head>
        <body style="margin:0;font-family:Segoe UI,Arial,sans-serif;background:#08111f;color:#f8fafc;">
            <div style="min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px;">
                <div style="width:100%;max-width:420px;background:#0f172acc;border:1px solid #334155;border-radius:24px;padding:28px;box-shadow:0 18px 48px rgba(0,0,0,0.35);">
                    <div style="font-size:14px;font-weight:700;color:#f97316;letter-spacing:0.06em;">D3BET</div>
                    <h1 style="margin:10px 0 8px;font-size:30px;">Přihlášení provozovatele</h1>
                    <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Přihlaste se svým provozním účtem a bezpečně otevřete interní správu kurzů, tiketů a vyhodnocení.</p>
                    {{errorMarkup}}
                    <form method="post" action="/account/login">
                        <input type="hidden" name="ReturnUrl" value="{{System.Net.WebUtility.HtmlEncode(safeReturnUrl)}}" />
                        <label style="display:block;margin-bottom:6px;color:#a7b6ca;font-weight:600;">Uživatelské jméno</label>
                        <input name="UserName" autocomplete="username" style="width:100%;box-sizing:border-box;margin-bottom:16px;padding:12px 14px;border-radius:12px;border:1px solid #334155;background:#0b1526;color:#f8fafc;" />
                        <label style="display:block;margin-bottom:6px;color:#a7b6ca;font-weight:600;">Heslo</label>
                        <input name="Password" type="password" autocomplete="current-password" style="width:100%;box-sizing:border-box;margin-bottom:22px;padding:12px 14px;border-radius:12px;border:1px solid #334155;background:#0b1526;color:#f8fafc;" />
                        <button type="submit" style="width:100%;padding:13px 16px;border:none;border-radius:14px;background:#f97316;color:white;font-weight:700;cursor:pointer;">Přihlásit a pokračovat</button>
                    </form>
                </div>
            </div>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/account/login", async (
    [FromForm] LoginFormModel loginModel,
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByNameAsync(loginModel.UserName);
    if (user is null)
    {
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(loginModel.ReturnUrl)}&error={Uri.EscapeDataString("Neplatné přihlašovací údaje.")}");
    }

    var result = await signInManager.PasswordSignInAsync(user, loginModel.Password, false, lockoutOnFailure: false);
    if (!result.Succeeded)
    {
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(loginModel.ReturnUrl)}&error={Uri.EscapeDataString("Neplatné přihlašovací údaje.")}");
    }

    if (!Uri.TryCreate(loginModel.ReturnUrl, UriKind.Relative, out _))
    {
        loginModel.ReturnUrl = "/";
    }

    return Results.LocalRedirect(loginModel.ReturnUrl);
});

app.MapMethods("/connect/authorize", new[] { "GET", "POST" }, async (
    HttpContext context,
    UserManager<IdentityUser> userManager,
    OperatorPrincipalFactory principalFactory) =>
{
    var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(context)
        ?? throw new InvalidOperationException("OAuth authorize požadavek se nepodařilo načíst.");

    var cookieAuthResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    if (!cookieAuthResult.Succeeded || cookieAuthResult.Principal is null)
    {
        var returnUrl = context.Request.Path + context.Request.QueryString;
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    var user = await userManager.GetUserAsync(cookieAuthResult.Principal);
    if (user is null)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        var returnUrl = context.Request.Path + context.Request.QueryString;
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    var principal = await principalFactory.CreateAsync(user, request.GetScopes(), context.RequestAborted);
    return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapPost("/connect/token", async (HttpContext context) =>
{
    var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(context)
        ?? throw new InvalidOperationException("OAuth pozadavek se nepodarilo nacist.");

    if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
        var principalFactory = context.RequestServices.GetRequiredService<OperatorPrincipalFactory>();
        var userId = result.Principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var tokenPrincipal = await principalFactory.CreateAsync(user, result.Principal.GetScopes(), context.RequestAborted);
        return Results.SignIn(tokenPrincipal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (!request.IsClientCredentialsGrantType())
    {
        return Results.BadRequest(new
        {
            error = OpenIddictConstants.Errors.UnsupportedGrantType,
            error_description = "Server zatím podporuje granty authorization_code, refresh_token a client_credentials."
        });
    }

    var applicationManager = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
    var application = await applicationManager.FindByClientIdAsync(request.ClientId ?? string.Empty);
    if (application is null)
    {
        return Results.BadRequest(new
        {
            error = "invalid_client",
            error_description = "OAuth klient nebyl nalezen."
        });
    }

    var identity = new System.Security.Claims.ClaimsIdentity(
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        OpenIddictConstants.Claims.Name,
        OpenIddictConstants.Claims.Role);

    identity.SetClaim(OpenIddictConstants.Claims.Subject, request.ClientId);
    identity.SetClaim(OpenIddictConstants.Claims.ClientId, request.ClientId);
    identity.SetClaim(OpenIddictConstants.Claims.Name, "D3Bet kiosk display");
    identity.SetScopes(request.GetScopes());
    identity.SetResources("d3bet-api");

    var principal = new System.Security.Claims.ClaimsPrincipal(identity);
    principal.SetScopes(request.GetScopes());
    principal.SetResources("d3bet-api");

    return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapGet("/api/customer-display", async (CustomerDisplayQueryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(cancellationToken)))
    .RequireAuthorization(Policies.DisplayRead);

app.MapGet("/api/auth/me", async (
    HttpContext context,
    UserManager<IdentityUser> userManager) =>
{
    var subject = context.User.GetClaim(OpenIddictConstants.Claims.Subject);
    if (string.IsNullOrWhiteSpace(subject))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Přihlášení je potřeba obnovit",
            detail: "V tokenu chybí identita přihlášeného provozovatele.");
    }

    var user = await userManager.FindByIdAsync(subject);
    if (user is null)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Přihlášený uživatel nebyl nalezen",
            detail: "Účet spojený s aktuální relací už na serveru D3Bet neexistuje.");
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Ok(new OperatorProfileResponse(user.Id, user.UserName ?? user.Id, roles.ToArray()));
}).RequireAuthorization(Policies.Operations);

var operations = app.MapGroup("/api/operations")
    .RequireAuthorization(Policies.Operations);

operations.MapGet("/dashboard", async (IMediator mediator, CancellationToken cancellationToken) =>
    Results.Ok(await mediator.Send(new GetDashboardQuery(), cancellationToken)));

operations.MapGet("/settings", async (ServerAppSettingsStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.LoadAsync(cancellationToken)));

operations.MapGet("/audit", async (
    AuditLogService auditLogService,
    [FromQuery] int? limit,
    CancellationToken cancellationToken) =>
    Results.Ok(await auditLogService.GetRecentAsync(limit ?? 60, cancellationToken)))
    .RequireAuthorization(Policies.AdminOnly);

operations.MapPut("/settings", async (
    HttpContext context,
    ServerAppSettingsStore store,
    AuditLogService auditLogService,
    UpdateAppSettingsRequest request,
    CancellationToken cancellationToken) =>
{
    await store.SaveAsync(request, cancellationToken);
    await auditLogService.RecordAsync(context, "SettingsUpdated", "AppSettings", "global", new
    {
        request.EnableAutoRefresh,
        request.AutoRefreshIntervalSeconds,
        request.EnableRealtimeRefresh,
        request.EnableTicketAnimations,
        request.EnableOperatorCommission,
        request.OperatorCommissionFormula,
        request.OperatorCommissionRatePercent,
        request.OperatorFlatFeePerBet
    }, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(Policies.AdminOnly);

operations.MapGet("/customer-display", async (CustomerDisplayQueryService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(cancellationToken)));

operations.MapPost("/markets", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    CreateBettingMarketRequest request,
    CancellationToken cancellationToken) =>
{
    var marketId = await mediator.Send(
        new CreateBettingMarketCommand(request.EventName, request.OpeningOdds, request.IsActive),
        cancellationToken);

    await auditLogService.RecordAsync(context, "MarketCreated", "BettingMarket", marketId.ToString(), new
    {
        request.EventName,
        request.OpeningOdds,
        request.IsActive
    }, cancellationToken);

    return Results.Created($"/api/operations/markets/{marketId}", new { id = marketId });
}).RequireAuthorization(Policies.AdminOnly);

operations.MapPut("/markets/{marketId:guid}", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    Guid marketId,
    UpdateBettingMarketRequest request,
    CancellationToken cancellationToken) =>
{
    await mediator.Send(
        new UpdateBettingMarketCommand(marketId, request.EventName, request.OpeningOdds, request.IsActive),
        cancellationToken);

    await auditLogService.RecordAsync(context, "MarketUpdated", "BettingMarket", marketId.ToString(), new
    {
        request.EventName,
        request.OpeningOdds,
        request.IsActive
    }, cancellationToken);

    return Results.NoContent();
}).RequireAuthorization(Policies.AdminOnly);

operations.MapPost("/bets", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    CreateBetRequest request,
    CancellationToken cancellationToken) =>
{
    var appliedOdds = await mediator.Send(
        new CreateBetCommand(request.MarketId, request.BettorId, request.BettorName, request.Stake, request.IsCommissionFeePaid),
        cancellationToken);

    await auditLogService.RecordAsync(context, "BetCreated", "Bet", request.MarketId.ToString(), new
    {
        request.MarketId,
        request.BettorId,
        request.BettorName,
        request.Stake,
        request.IsCommissionFeePaid,
        AppliedOdds = appliedOdds
    }, cancellationToken);

    return Results.Ok(new { appliedOdds });
});

operations.MapPut("/bets/{betId:guid}", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    Guid betId,
    UpdateBetRequest request,
    CancellationToken cancellationToken) =>
{
    var appliedOdds = await mediator.Send(
        new UpdateBetCommand(betId, request.MarketId, request.BettorId, request.BettorName, request.Stake, request.IsCommissionFeePaid),
        cancellationToken);

    await auditLogService.RecordAsync(context, "BetUpdated", "Bet", betId.ToString(), new
    {
        request.MarketId,
        request.BettorId,
        request.BettorName,
        request.Stake,
        request.IsCommissionFeePaid,
        AppliedOdds = appliedOdds
    }, cancellationToken);

    return Results.Ok(new { appliedOdds });
});

operations.MapDelete("/bets/{betId:guid}", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    Guid betId,
    CancellationToken cancellationToken) =>
{
    await mediator.Send(new DeleteBetCommand(betId), cancellationToken);
    await auditLogService.RecordAsync(context, "BetDeleted", "Bet", betId.ToString(), cancellationToken: cancellationToken);
    return Results.NoContent();
});

operations.MapPost("/bets/{betId:guid}/outcome", async (
    HttpContext context,
    IMediator mediator,
    AuditLogService auditLogService,
    Guid betId,
    SetBetOutcomeStatusRequest request,
    CancellationToken cancellationToken) =>
{
    await mediator.Send(new SetBetOutcomeStatusCommand(betId, request.OutcomeStatus), cancellationToken);
    await auditLogService.RecordAsync(context, "BetOutcomeChanged", "Bet", betId.ToString(), new
    {
        request.OutcomeStatus
    }, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/api/health", () => Results.Ok(new
{
    application = "D3Bet Server",
    utcTime = DateTime.UtcNow
}));

app.MapHub<BetsHub>(BetsHub.HubRoute);

app.Run();

static bool ShouldWriteApiProblem(HttpContext context)
{
    return context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}

static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
{
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";

    return context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail,
        Type = $"https://httpstatuses.com/{statusCode}",
        Instance = context.Request.Path,
        Extensions =
        {
            ["traceId"] = context.TraceIdentifier
        }
    });
}

static string ResolveCorrelationId(HttpContext context, string headerName)
{
    if (context.Request.Headers.TryGetValue(headerName, out StringValues existingValues))
    {
        var existing = existingValues.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing.Trim();
        }
    }

    return Guid.NewGuid().ToString("N");
}
