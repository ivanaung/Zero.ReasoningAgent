using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;
using ScheduleApp.Services;

namespace ScheduleApp.Controllers;

[Route("account")]
public class AccountController(
    IGoogleIntegrationService googleIntegrationService,
    IUserAccountService userAccountService,
    ICurrentUserService currentUserService,
    IGoogleOAuthRedirectUriBuilder googleOAuthRedirectUriBuilder,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ClearAppAuthCookies();

        return View(await BuildLoginViewModelAsync(returnUrl, cancellationToken: cancellationToken));
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken = default)
    {
        model.ReturnUrl = string.IsNullOrWhiteSpace(model.ReturnUrl) ? Url.Action("Index", "Home") : model.ReturnUrl;
        if (!ModelState.IsValid)
        {
            await PopulateGoogleAvailabilityAsync(model, cancellationToken);
            return View(model);
        }

        var user = await userAccountService.ValidateCredentialsAsync(model.Username, model.Password, cancellationToken);
        if (user == null)
        {
            TempData["LoginError"] = "Invalid username or password.";
            await PopulateGoogleAvailabilityAsync(model, cancellationToken);
            return View(model);
        }

        await SignInUserAsync(user.Id, user.DisplayName, user.Email, user.Role);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("google-signin")]
    public async Task<IActionResult> GoogleSignIn([FromForm] string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var status = await googleIntegrationService.GetStatusAsync(cancellationToken);
        if (!status.IsConfigured)
        {
            TempData["LoginError"] = "Google sign-in is not available.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var callbackUrl = await googleOAuthRedirectUriBuilder.BuildCallbackUriAsync(Request, cancellationToken);
        var destination = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("Index", "Home")! : returnUrl!;
        logger.LogInformation("Starting Google sign-in with redirect URI {RedirectUri}", callbackUrl);
        var authorizationUrl = await googleIntegrationService.BuildAuthorizationUrlAsync(callbackUrl, destination, cancellationToken);
        if (string.IsNullOrWhiteSpace(authorizationUrl))
        {
            TempData["LoginError"] = "Google sign-in is not available.";
            return RedirectToAction(nameof(Login), new { returnUrl = destination });
        }

        return Redirect(authorizationUrl);
    }

    [AllowAnonymous]
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken = default)
    {
        var returnUrl = string.IsNullOrWhiteSpace(state) ? Url.Action("Index", "Home")! : state;

        if (!string.IsNullOrWhiteSpace(error))
        {
            logger.LogWarning("Google sign-in returned error {GoogleError}", error);
            TempData["LoginError"] = "Google sign-in failed. Use local login or check Google settings.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["LoginError"] = "Google sign-in failed. Use local login or check Google settings.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        try
        {
            var callbackUrl = await googleOAuthRedirectUriBuilder.BuildCallbackUriAsync(Request, cancellationToken);
            var preferredUserId = currentUserService.IsAuthenticated ? currentUserService.UserId : null;
            var profile = await googleIntegrationService.CompleteSignInAsync(code, callbackUrl, preferredUserId, cancellationToken);
            var appUser = await userAccountService.GetByIdAsync(profile.UserId, cancellationToken)
                ?? throw new InvalidOperationException("No local user was created for the Google account.");
            await SignInUserAsync(appUser.Id, appUser.DisplayName, appUser.Email, appUser.Role, profile.PictureUrl);

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google sign-in callback failed.");
            TempData["LoginError"] = "Google sign-in failed. Use local login or check Google settings.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearAppAuthCookies();
        return RedirectToAction(nameof(Login));
    }

    private void ClearAppAuthCookies()
    {
        Response.Cookies.Delete("ScheduleApp.Auth");
        Response.Cookies.Delete("ScheduleApp.Antiforgery");
        Response.Cookies.Delete("ScheduleApp.Auth", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("ScheduleApp.Antiforgery", new CookieOptions { Path = "/" });

        if (Request.PathBase.HasValue)
        {
            var pathBase = Request.PathBase.Value!;
            Response.Cookies.Delete("ScheduleApp.Auth", new CookieOptions { Path = pathBase });
            Response.Cookies.Delete("ScheduleApp.Antiforgery", new CookieOptions { Path = pathBase });
        }
    }

    private async Task<LoginViewModel> BuildLoginViewModelAsync(
        string? returnUrl,
        string username = "",
        CancellationToken cancellationToken = default)
    {
        var model = new LoginViewModel
        {
            Username = username,
            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("Index", "Home") : returnUrl
        };
        await PopulateGoogleAvailabilityAsync(model, cancellationToken);
        return model;
    }

    private async Task PopulateGoogleAvailabilityAsync(LoginViewModel model, CancellationToken cancellationToken)
    {
        var status = await googleIntegrationService.GetStatusAsync(cancellationToken);
        model.ShowGoogleLogin = status.IsConfigured;
    }

    private async Task SignInUserAsync(string userId, string displayName, string? email, string role, string? pictureUrl = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (!string.IsNullOrWhiteSpace(pictureUrl))
        {
            claims.Add(new Claim("picture", pictureUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }
}
