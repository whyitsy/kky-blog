using System.Security.Cryptography;
using System.Text.Json;
using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Blog.WebApi.Security
{
    /// <summary>
    /// 第三方登录用的两类短期 cookie：
    ///   - state：OAuth 上下文（state / PKCE verifier / intent / returnUrl）；
    ///   - login ticket：回调成功后的一次性本站登录票据，前端拿它换 JWT。
    ///
    /// 两者都用 Data Protection 加密，HttpOnly + SameSite=Lax。
    /// state 必须是 Lax：OAuth callback 是第三方发起的顶层 GET，Strict 不会发送。
    /// </summary>
    public sealed class ExternalAuthCookieStore
    {
        private const string StateCookieName = "blog_external_state";
        private const string LoginTicketCookieName = "blog_external_login";
        private const string CookiePath = "/api/auth";
        private const string ProtectorPurpose = "Blog.ExternalAuth.Cookie.v1";

        private readonly IDataProtector _protector;
        private readonly ExternalAuthOptions _options;

        public ExternalAuthCookieStore(
            IDataProtectionProvider dataProtectionProvider,
            ExternalAuthOptions options)
        {
            _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
            _options = options;
        }

        public void SetState(HttpResponse response, ExternalAuthState state)
        {
            SetCookie(response, StateCookieName, state, TimeSpan.FromMinutes(Math.Clamp(_options.StateMinutes, 1, 30)));
        }

        public bool TryGetState(HttpRequest request, out ExternalAuthState? state) =>
            TryGetCookie(request, StateCookieName, out state);

        public void ClearState(HttpResponse response) => DeleteCookie(response, StateCookieName);

        public void SetLoginTicket(HttpResponse response, Guid userId)
        {
            var ticket = new ExternalLoginTicket(
                userId,
                DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.LoginTicketMinutes, 1, 10)));

            SetCookie(response, LoginTicketCookieName, ticket, TimeSpan.FromMinutes(Math.Clamp(_options.LoginTicketMinutes, 1, 10)));
        }

        public bool TryGetLoginTicket(HttpRequest request, out Guid userId)
        {
            userId = Guid.Empty;
            if (!TryGetCookie(request, LoginTicketCookieName, out ExternalLoginTicket? ticket) || ticket is null)
                return false;

            if (ticket.ExpiresAt <= DateTimeOffset.UtcNow || ticket.UserId == Guid.Empty)
                return false;

            userId = ticket.UserId;
            return true;
        }

        public void ClearLoginTicket(HttpResponse response) => DeleteCookie(response, LoginTicketCookieName);

        // ---------------------------------------------------------------- helpers

        private void SetCookie<T>(HttpResponse response, string name, T value, TimeSpan maxAge)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(value);
            var protectedBytes = _protector.Protect(json);
            var cookieValue = WebEncoders.Base64UrlEncode(protectedBytes);

            response.Cookies.Append(name, cookieValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = CookiePath,
                MaxAge = maxAge,
                IsEssential = true,
            });
        }

        private bool TryGetCookie<T>(HttpRequest request, string name, out T? value)
        {
            value = default;
            if (!request.Cookies.TryGetValue(name, out var cookieValue) || string.IsNullOrWhiteSpace(cookieValue))
                return false;

            try
            {
                var protectedBytes = WebEncoders.Base64UrlDecode(cookieValue);
                var json = _protector.Unprotect(protectedBytes);
                value = JsonSerializer.Deserialize<T>(json);
                return value is not null;
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException)
            {
                return false;
            }
        }

        private static void DeleteCookie(HttpResponse response, string name)
        {
            response.Cookies.Delete(name, new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = CookiePath,
            });
        }

        private sealed record ExternalLoginTicket(Guid UserId, DateTimeOffset ExpiresAt);
    }
}
