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
        options.SignIn.RequireConfirmedAccount = true;
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
        if (builder.Environment.IsDevelopment())
        {
            options.DisableTransportSecurityRequirement();
        }
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
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
builder.Services.AddSingleton<AccountPageRenderer>();
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

app.MapGet("/account/login", (
    [FromQuery] string? returnUrl,
    [FromQuery] string? error,
    [FromQuery] string? info,
    AccountPageRenderer renderer) =>
{
    var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    return Results.Content(renderer.RenderLogin(safeReturnUrl, error, info), "text/html; charset=utf-8");
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
        if (!user.EmailConfirmed)
        {
            return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(loginModel.ReturnUrl)}&error={Uri.EscapeDataString("Účet ještě není aktivní. Dokončete aktivaci nebo si vyžádejte nový aktivační odkaz.")}");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(loginModel.ReturnUrl)}&error={Uri.EscapeDataString("Účet je dočasně uzamčený. Obnovte přístup přes reaktivaci nebo reset hesla.")}");
        }

        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString(loginModel.ReturnUrl)}&error={Uri.EscapeDataString("Neplatné přihlašovací údaje.")}");
    }

    if (!Uri.TryCreate(loginModel.ReturnUrl, UriKind.Relative, out _))
    {
        loginModel.ReturnUrl = "/";
    }

    return Results.LocalRedirect(loginModel.ReturnUrl);
});

app.MapGet("/account/register", (AccountPageRenderer renderer) =>
    Results.Content(renderer.RenderRegister(new RegisterAccountFormModel()), "text/html; charset=utf-8"));

app.MapPost("/account/register", async (
    [FromForm] RegisterAccountFormModel model,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    if (string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
    {
        return Results.Content(renderer.RenderRegister(model, error: "Vyplňte uživatelské jméno i heslo."), "text/html; charset=utf-8");
    }

    if (!string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.Content(renderer.RenderRegister(model, error: "Hesla se neshodují."), "text/html; charset=utf-8");
    }

    var existingUser = await userManager.FindByNameAsync(model.UserName.Trim());
    if (existingUser is not null)
    {
        return Results.Content(renderer.RenderRegister(model, error: "Zadané uživatelské jméno už existuje."), "text/html; charset=utf-8");
    }

    if (!string.IsNullOrWhiteSpace(model.Email))
    {
        var emailOwner = await userManager.FindByEmailAsync(model.Email.Trim());
        if (emailOwner is not null)
        {
            return Results.Content(renderer.RenderRegister(model, error: "Zadaný e-mail už používá jiný účet."), "text/html; charset=utf-8");
        }
    }

    var user = new IdentityUser
    {
        UserName = model.UserName.Trim(),
        Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
        EmailConfirmed = false
    };

    var createResult = await userManager.CreateAsync(user, model.Password);
    if (!createResult.Succeeded)
    {
        return Results.Content(
            renderer.RenderRegister(model, error: string.Join(" ", createResult.Errors.Select(error => error.Description))),
            "text/html; charset=utf-8");
    }

    await userManager.AddToRoleAsync(user, Roles.Customer);
    var activationLink = await BuildActivationLinkAsync(context, userManager, user);
    await auditLogService.RecordAsync(context, "SelfServiceRegistered", "IdentityUser", user.Id, new
    {
        user.UserName,
        user.Email
    });

    return Results.Content(
        renderer.RenderRegister(
            new RegisterAccountFormModel(),
            info: "Účet byl vytvořen. Dokončete aktivaci přes připravený odkaz.",
            activationLink: activationLink),
        "text/html; charset=utf-8");
});

app.MapGet("/account/reactivate", (AccountPageRenderer renderer) =>
    Results.Content(renderer.RenderReactivate(), "text/html; charset=utf-8"));

app.MapPost("/account/reactivate", async (
    [FromForm] ReactivateAccountFormModel model,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    var user = await FindUserByIdentifierAsync(userManager, model.UserNameOrEmail);
    if (user is null)
    {
        return Results.Content(renderer.RenderReactivate(model.UserNameOrEmail, error: "Takový účet se nepodařilo najít."), "text/html; charset=utf-8");
    }

    if (user.EmailConfirmed)
    {
        return Results.Content(renderer.RenderReactivate(model.UserNameOrEmail, info: "Tento účet je už aktivní. Můžete se rovnou přihlásit."), "text/html; charset=utf-8");
    }

    var activationLink = await BuildActivationLinkAsync(context, userManager, user);
    await auditLogService.RecordAsync(context, "SelfServiceReactivationLinkIssued", "IdentityUser", user.Id, new
    {
        user.UserName,
        user.Email
    });

    return Results.Content(
        renderer.RenderReactivate(model.UserNameOrEmail, info: "Nový aktivační odkaz je připraven.", activationLink: activationLink),
        "text/html; charset=utf-8");
});

app.MapGet("/account/activate", async (
    [FromQuery] string userId,
    [FromQuery] string token,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
    {
        return Results.Content(renderer.RenderActivationResult("Aktivace se nezdařila", "Zadaný účet se nepodařilo dohledat."), "text/html; charset=utf-8");
    }

    if (user.EmailConfirmed)
    {
        return Results.Content(renderer.RenderActivationResult("Účet už je aktivní", "Tento účet už byl aktivován dříve. Můžete se přihlásit."), "text/html; charset=utf-8");
    }

    var result = await userManager.ConfirmEmailAsync(user, token);
    if (!result.Succeeded)
    {
        return Results.Content(
            renderer.RenderActivationResult(
                "Aktivace se nezdařila",
                string.Join(" ", result.Errors.Select(error => error.Description)),
                "Vyžádat nový aktivační odkaz",
                "/account/reactivate"),
            "text/html; charset=utf-8");
    }

    await auditLogService.RecordAsync(context, "SelfServiceActivated", "IdentityUser", user.Id, new
    {
        user.UserName
    });

    return Results.Content(renderer.RenderActivationResult("Účet je aktivní", "Aktivace proběhla úspěšně. Teď se můžete přihlásit do D3Bet."), "text/html; charset=utf-8");
});

app.MapGet("/account/forgot-password", (AccountPageRenderer renderer) =>
    Results.Content(renderer.RenderForgotPassword(), "text/html; charset=utf-8"));

app.MapPost("/account/forgot-password", async (
    [FromForm] ForgotPasswordFormModel model,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    var user = await FindUserByIdentifierAsync(userManager, model.UserNameOrEmail);
    if (user is null)
    {
        return Results.Content(renderer.RenderForgotPassword(model.UserNameOrEmail, error: "Takový účet se nepodařilo najít."), "text/html; charset=utf-8");
    }

    if (!user.EmailConfirmed)
    {
        return Results.Content(renderer.RenderForgotPassword(model.UserNameOrEmail, error: "Účet ještě není aktivní. Nejprve dokončete aktivaci nebo si vyžádejte nový aktivační odkaz."), "text/html; charset=utf-8");
    }

    var token = await userManager.GeneratePasswordResetTokenAsync(user);
    var resetLink = BuildAbsoluteUrl(context, "/account/reset-password", new Dictionary<string, string?>
    {
        ["userId"] = user.Id,
        ["token"] = token
    });

    await auditLogService.RecordAsync(context, "SelfServicePasswordResetLinkIssued", "IdentityUser", user.Id, new
    {
        user.UserName
    });

    return Results.Content(
        renderer.RenderForgotPassword(model.UserNameOrEmail, info: "Odkaz pro reset hesla je připraven.", resetLink: resetLink),
        "text/html; charset=utf-8");
});

app.MapGet("/account/reset-password", (
    [FromQuery] string userId,
    [FromQuery] string token,
    AccountPageRenderer renderer) =>
{
    var model = new ResetPasswordFormModel
    {
        UserId = userId,
        Token = token
    };

    return Results.Content(renderer.RenderResetPassword(model), "text/html; charset=utf-8");
});

app.MapPost("/account/reset-password", async (
    [FromForm] ResetPasswordFormModel model,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    if (!string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.Content(renderer.RenderResetPassword(model, error: "Hesla se neshodují."), "text/html; charset=utf-8");
    }

    var user = await userManager.FindByIdAsync(model.UserId);
    if (user is null)
    {
        return Results.Content(renderer.RenderResetPassword(model, error: "Účet už neexistuje."), "text/html; charset=utf-8");
    }

    var result = await userManager.ResetPasswordAsync(user, model.Token, model.Password);
    if (!result.Succeeded)
    {
        return Results.Content(
            renderer.RenderResetPassword(model, error: string.Join(" ", result.Errors.Select(error => error.Description))),
            "text/html; charset=utf-8");
    }

    await auditLogService.RecordAsync(context, "SelfServicePasswordResetCompleted", "IdentityUser", user.Id, new
    {
        user.UserName
    });

    return Results.Content(renderer.RenderActivationResult("Heslo bylo změněno", "Nové heslo bylo úspěšně nastaveno. Můžete se znovu přihlásit."), "text/html; charset=utf-8");
});

app.MapGet("/account/profile", async (
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer) =>
{
    var principal = await EnsureCookieUserAsync(context);
    if (principal is null)
    {
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString("/account/profile")}");
    }

    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.Redirect("/account/login?error=" + Uri.EscapeDataString("Relace vypršela. Přihlaste se prosím znovu."));
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Content(
        renderer.RenderProfile(
            new UpdateProfileFormModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty
            },
            user.UserName ?? user.Id,
            string.Join(", ", roles)),
        "text/html; charset=utf-8");
});

app.MapPost("/account/profile", async (
    [FromForm] UpdateProfileFormModel model,
    HttpContext context,
    UserManager<IdentityUser> userManager,
    AccountPageRenderer renderer,
    AuditLogService auditLogService) =>
{
    var principal = await EnsureCookieUserAsync(context);
    if (principal is null)
    {
        return Results.Redirect($"/account/login?returnUrl={Uri.EscapeDataString("/account/profile")}");
    }

    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.Redirect("/account/login?error=" + Uri.EscapeDataString("Relace vypršela. Přihlaste se prosím znovu."));
    }

    var roles = await userManager.GetRolesAsync(user);
    var rolesDisplay = string.Join(", ", roles);

    if (string.IsNullOrWhiteSpace(model.UserName))
    {
        return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: "Uživatelské jméno nesmí být prázdné."), "text/html; charset=utf-8");
    }

    if (!string.Equals(model.UserName.Trim(), user.UserName, StringComparison.Ordinal))
    {
        var setUserNameResult = await userManager.SetUserNameAsync(user, model.UserName.Trim());
        if (!setUserNameResult.Succeeded)
        {
            return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: string.Join(" ", setUserNameResult.Errors.Select(error => error.Description))), "text/html; charset=utf-8");
        }
    }

    var normalizedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
    if (!string.Equals(normalizedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
    {
        user.Email = normalizedEmail;
        user.NormalizedEmail = normalizedEmail?.ToUpperInvariant();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: string.Join(" ", updateResult.Errors.Select(error => error.Description))), "text/html; charset=utf-8");
        }
    }

    if (!string.IsNullOrWhiteSpace(model.NewPassword))
    {
        if (!string.Equals(model.NewPassword, model.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: "Nová hesla se neshodují."), "text/html; charset=utf-8");
        }

        if (string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: "Pro změnu hesla vyplňte i aktuální heslo."), "text/html; charset=utf-8");
        }

        var changePasswordResult = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            return Results.Content(renderer.RenderProfile(model, user.UserName ?? user.Id, rolesDisplay, error: string.Join(" ", changePasswordResult.Errors.Select(error => error.Description))), "text/html; charset=utf-8");
        }
    }

    await auditLogService.RecordAsync(context, "SelfServiceProfileUpdated", "IdentityUser", user.Id, new
    {
        user.UserName,
        user.Email,
        PasswordChanged = !string.IsNullOrWhiteSpace(model.NewPassword)
    });

    return Results.Content(
        renderer.RenderProfile(
            new UpdateProfileFormModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty
            },
            user.UserName ?? user.Id,
            rolesDisplay,
            info: "Profil byl úspěšně uložen."),
        "text/html; charset=utf-8");
});

app.MapGet("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(IdentityConstants.ApplicationScheme);
    return Results.Redirect("/account/login?info=" + Uri.EscapeDataString("Byli jste úspěšně odhlášeni."));
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

static async Task<string> BuildActivationLinkAsync(HttpContext context, UserManager<IdentityUser> userManager, IdentityUser user)
{
    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
    return BuildAbsoluteUrl(context, "/account/activate", new Dictionary<string, string?>
    {
        ["userId"] = user.Id,
        ["token"] = token
    });
}

static async Task<IdentityUser?> FindUserByIdentifierAsync(UserManager<IdentityUser> userManager, string? identifier)
{
    if (string.IsNullOrWhiteSpace(identifier))
    {
        return null;
    }

    var normalized = identifier.Trim();
    return await userManager.FindByNameAsync(normalized) ?? await userManager.FindByEmailAsync(normalized);
}

static string BuildAbsoluteUrl(HttpContext context, string path, IReadOnlyDictionary<string, string?> query)
{
    var baseUri = $"{context.Request.Scheme}://{context.Request.Host}";
    var queryString = string.Join("&", query
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
        .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

    return string.IsNullOrWhiteSpace(queryString)
        ? $"{baseUri}{path}"
        : $"{baseUri}{path}?{queryString}";
}

static async Task<System.Security.Claims.ClaimsPrincipal?> EnsureCookieUserAsync(HttpContext context)
{
    var authenticationResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    return authenticationResult.Succeeded ? authenticationResult.Principal : null;
}
