using System.Net;
using BettingApp.Server.Models;

namespace BettingApp.Server.Services;

public sealed class AccountPageRenderer
{
    public string RenderLogin(string returnUrl, string? error = null, string? info = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Přihlášení do D3Bet</h1>
            <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Přihlaste se svým účtem a otevřete bezpečný provozní přehled, správu sázek i administraci.</p>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "info")}
            <form method="post" action="/account/login">
                <input type="hidden" name="ReturnUrl" value="{Encode(returnUrl)}" />
                {RenderInput("Uživatelské jméno", "UserName", string.Empty, autocomplete: "username")}
                {RenderInput("Heslo", "Password", string.Empty, type: "password", autocomplete: "current-password")}
                <button type="submit" style="{PrimaryButtonStyle}">Přihlásit a pokračovat</button>
            </form>
            {RenderLinks(
                ("/account/register", "Nemáte účet? Založte si ho"),
                ("/account/reactivate", "Potřebujete aktivovat nebo znovu aktivovat účet?"),
                ("/account/forgot-password", "Zapomněli jste heslo?"),
                ("/account/profile", "Správa profilu"))}
            """;

        return RenderLayout("D3Bet Přihlášení", body);
    }

    public string RenderRegister(RegisterAccountFormModel model, string? error = null, string? info = null, string? activationLink = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Založení účtu</h1>
            <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Vytvořte si vlastní přístup do prostředí D3Bet a připravte účet k aktivaci.</p>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "success")}
            {RenderActionLink("Aktivační odkaz", activationLink)}
            <form method="post" action="/account/register">
                {RenderInput("Uživatelské jméno", "UserName", model.UserName, autocomplete: "username")}
                {RenderInput("E-mail", "Email", model.Email, type: "email", autocomplete: "email")}
                {RenderInput("Heslo", "Password", string.Empty, type: "password", autocomplete: "new-password")}
                {RenderInput("Potvrzení hesla", "ConfirmPassword", string.Empty, type: "password", autocomplete: "new-password")}
                <button type="submit" style="{PrimaryButtonStyle}">Vytvořit účet</button>
            </form>
            {RenderLinks(("/account/login", "Zpět na přihlášení"))}
            """;

        return RenderLayout("D3Bet Založení účtu", body);
    }

    public string RenderReactivate(string identifier = "", string? error = null, string? info = null, string? activationLink = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Aktivace a reaktivace účtu</h1>
            <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Nechte si znovu připravit aktivační odkaz pro účet, který ještě není aktivní.</p>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "success")}
            {RenderActionLink("Aktivační odkaz", activationLink)}
            <form method="post" action="/account/reactivate">
                {RenderInput("Uživatelské jméno nebo e-mail", "UserNameOrEmail", identifier)}
                <button type="submit" style="{PrimaryButtonStyle}">Poslat nový aktivační odkaz</button>
            </form>
            {RenderLinks(("/account/login", "Zpět na přihlášení"), ("/account/register", "Vytvořit nový účet"))}
            """;

        return RenderLayout("D3Bet Aktivace účtu", body);
    }

    public string RenderForgotPassword(string identifier = "", string? error = null, string? info = null, string? resetLink = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Obnovení hesla</h1>
            <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Připravte si bezpečný odkaz pro nastavení nového hesla.</p>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "success")}
            {RenderActionLink("Odkaz pro reset hesla", resetLink)}
            <form method="post" action="/account/forgot-password">
                {RenderInput("Uživatelské jméno nebo e-mail", "UserNameOrEmail", identifier)}
                <button type="submit" style="{PrimaryButtonStyle}">Vygenerovat odkaz</button>
            </form>
            {RenderLinks(("/account/login", "Zpět na přihlášení"))}
            """;

        return RenderLayout("D3Bet Obnovení hesla", body);
    }

    public string RenderResetPassword(ResetPasswordFormModel model, string? error = null, string? info = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Reset hesla</h1>
            <p style="margin:0 0 22px;color:#a7b6ca;line-height:1.5;">Nastavte nové heslo pro svůj účet a vraťte se zpět do provozu.</p>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "success")}
            <form method="post" action="/account/reset-password">
                <input type="hidden" name="UserId" value="{Encode(model.UserId)}" />
                <input type="hidden" name="Token" value="{Encode(model.Token)}" />
                {RenderInput("Nové heslo", "Password", string.Empty, type: "password", autocomplete: "new-password")}
                {RenderInput("Potvrzení nového hesla", "ConfirmPassword", string.Empty, type: "password", autocomplete: "new-password")}
                <button type="submit" style="{PrimaryButtonStyle}">Uložit nové heslo</button>
            </form>
            {RenderLinks(("/account/login", "Zpět na přihlášení"))}
            """;

        return RenderLayout("D3Bet Reset hesla", body);
    }

    public string RenderActivationResult(string title, string message, string linkLabel = "Přejít na přihlášení", string linkUrl = "/account/login")
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">{Encode(title)}</h1>
            <div style="margin:18px 0;padding:14px 16px;border-radius:14px;background:#10213a;border:1px solid #1d4ed8;color:#dbeafe;">
                {Encode(message)}
            </div>
            <div style="margin-top:16px;"><a href="{Encode(linkUrl)}" style="{LinkStyle}">{Encode(linkLabel)}</a></div>
            """;

        return RenderLayout($"D3Bet {title}", body);
    }

    public string RenderProfile(UpdateProfileFormModel model, string displayName, string roles, string? error = null, string? info = null)
    {
        var body = $"""
            <h1 style="margin:10px 0 8px;font-size:30px;">Správa profilu</h1>
            <p style="margin:0 0 10px;color:#a7b6ca;line-height:1.5;">Udržujte svůj účet aktuální a bezpečný. Zde můžete změnit identifikační údaje i heslo.</p>
            <div style="margin:0 0 18px;padding:12px 14px;border-radius:14px;background:#0b1526;border:1px solid #334155;color:#cbd5e1;">
                Přihlášený uživatel: <strong>{Encode(displayName)}</strong><br />
                Role: <strong>{Encode(roles)}</strong>
            </div>
            {RenderNotice(error, "error")}
            {RenderNotice(info, "success")}
            <form method="post" action="/account/profile">
                {RenderInput("Uživatelské jméno", "UserName", model.UserName, autocomplete: "username")}
                {RenderInput("E-mail", "Email", model.Email, type: "email", autocomplete: "email")}
                <div style="height:1px;background:#2234475e;margin:18px 0;"></div>
                <div style="margin-bottom:10px;color:#a7b6ca;">Změna hesla je volitelná. Pokud nové heslo necháte prázdné, profil se uloží bez jeho změny.</div>
                {RenderInput("Aktuální heslo", "CurrentPassword", string.Empty, type: "password", autocomplete: "current-password")}
                {RenderInput("Nové heslo", "NewPassword", string.Empty, type: "password", autocomplete: "new-password")}
                {RenderInput("Potvrzení nového hesla", "ConfirmNewPassword", string.Empty, type: "password", autocomplete: "new-password")}
                <button type="submit" style="{PrimaryButtonStyle}">Uložit profil</button>
            </form>
            {RenderLinks(("/account/logout", "Odhlásit se"), ("/account/login", "Přihlášení"))}
            """;

        return RenderLayout("D3Bet Profil", body);
    }

    private static string RenderLayout(string title, string body)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="cs">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{{Encode(title)}}</title>
            </head>
            <body style="margin:0;font-family:Segoe UI,Arial,sans-serif;background:#08111f;color:#f8fafc;">
                <div style="min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px;">
                    <div style="width:100%;max-width:560px;background:#0f172acc;border:1px solid #334155;border-radius:24px;padding:28px;box-shadow:0 18px 48px rgba(0,0,0,0.35);">
                        <div style="font-size:14px;font-weight:700;color:#f97316;letter-spacing:0.06em;">D3BET</div>
                        {{body}}
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string RenderInput(string label, string name, string value, string type = "text", string autocomplete = "off")
    {
        return $"""
            <label style="display:block;margin-bottom:6px;color:#a7b6ca;font-weight:600;">{Encode(label)}</label>
            <input name="{Encode(name)}" value="{Encode(value)}" type="{Encode(type)}" autocomplete="{Encode(autocomplete)}" style="width:100%;box-sizing:border-box;margin-bottom:16px;padding:12px 14px;border-radius:12px;border:1px solid #334155;background:#0b1526;color:#f8fafc;" />
            """;
    }

    private static string RenderNotice(string? message, string tone)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var (background, foreground, border) = tone switch
        {
            "error" => ("#3f1d1d", "#fecaca", "#7f1d1d"),
            "success" => ("#10261a", "#bbf7d0", "#166534"),
            _ => ("#10213a", "#dbeafe", "#1d4ed8")
        };

        return $"""<div style="margin-bottom:16px;padding:12px 14px;border-radius:12px;background:{background};color:{foreground};border:1px solid {border};">{Encode(message)}</div>""";
    }

    private static string RenderActionLink(string title, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return $"""
            <div style="margin-bottom:16px;padding:12px 14px;border-radius:12px;background:#10261a;color:#bbf7d0;border:1px solid #166534;">
                <strong>{Encode(title)}:</strong><br />
                <a href="{Encode(url)}" style="{LinkStyle}">{Encode(url)}</a>
            </div>
            """;
    }

    private static string RenderLinks(params (string Url, string Label)[] links)
    {
        if (links.Length == 0)
        {
            return string.Empty;
        }

        var items = string.Join(string.Empty, links.Select(link =>
            $"""<a href="{Encode(link.Url)}" style="{LinkStyle}">{Encode(link.Label)}</a><br />"""));

        return $"""<div style="margin-top:18px;color:#93c5fd;line-height:1.9;">{items}</div>""";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string PrimaryButtonStyle = "width:100%;padding:13px 16px;border:none;border-radius:14px;background:#f97316;color:white;font-weight:700;cursor:pointer;";
    private const string LinkStyle = "color:#93c5fd;text-decoration:none;font-weight:600;";
}
