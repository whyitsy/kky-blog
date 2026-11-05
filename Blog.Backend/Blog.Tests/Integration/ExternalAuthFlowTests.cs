using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.Infrastructure.Persistence;
using Blog.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blog.Tests.Integration;

/// <summary>
/// 第三方登录的完整 HTTP 闭环：start（写 state cookie）→ callback（写一次性票据）
/// → complete（换 JWT）→ /me。provider 用 fake，验证的是本站的 cookie/state/票据逻辑。
/// </summary>
[Collection(BlogApiCollection.Name)]
public sealed class ExternalAuthFlowTests
{
    private readonly BlogApiFixture _fixture;

    public ExternalAuthFlowTests(BlogApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task 从127访问GitHub登录_先重定向到回调配置的localhost()
    {
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ExternalAuth:FrontendBaseUrl"] = "http://localhost",
                    ["ExternalAuth:GitHub:Enabled"] = "true",
                    ["ExternalAuth:GitHub:ClientId"] = "test-client",
                    ["ExternalAuth:GitHub:ClientSecret"] = "test-secret",
                    ["ExternalAuth:GitHub:RedirectUri"] = "http://localhost/api/auth/external/github/callback",
                });
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/auth/external/github/start?returnUrl=/admin/security");
        request.Headers.Host = "127.0.0.1:8080";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("localhost", location.Host);
        Assert.Contains("/api/auth/external/github/start", location.AbsolutePath);
        Assert.Contains("returnUrl=%2Fadmin%2Fsecurity", location.Query);
    }

    [Fact]
    public async Task 已绑定的GitHub账号_走完回调_可以换到本站JWT()
    {
        var adminId = await EnsureAdminBindingAsync();

        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ExternalAuth:FrontendBaseUrl"] = "http://localhost",
                    ["ExternalAuth:GitHub:Enabled"] = "true",
                    ["ExternalAuth:GitHub:ClientId"] = "test-client",
                    ["ExternalAuth:GitHub:ClientSecret"] = "test-secret",
                    ["ExternalAuth:GitHub:RedirectUri"] = "http://localhost/api/auth/external/github/callback",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IExternalAuthProvider>();
                services.AddSingleton<IExternalAuthProvider>(new FakeGitHubProvider());
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var start = await client.GetAsync("/api/auth/external/github/start?returnUrl=/admin/security");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var authorizeUrl = start.Headers.Location?.ToString();
        Assert.NotNull(authorizeUrl);

        var state = QueryHelpers.ParseQuery(new Uri(authorizeUrl!).Query)["state"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(state));

        var callback = await client.GetAsync(
            $"/api/auth/external/github/callback?code=fake-code&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("/auth/callback", callback.Headers.Location!.ToString());

        var complete = await client.PostAsync("/api/auth/external/complete", content: null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        using var doc = JsonDocument.Parse(await complete.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(adminId, meDoc.RootElement.GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task 管理员绑定飞书_回调后写入绑定并可解绑()
    {
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ExternalAuth:FrontendBaseUrl"] = "http://localhost",
                    ["ExternalAuth:GitHub:Enabled"] = "true",
                    ["ExternalAuth:GitHub:ClientId"] = "test-client",
                    ["ExternalAuth:GitHub:ClientSecret"] = "test-secret",
                    ["ExternalAuth:GitHub:RedirectUri"] = "http://localhost/api/auth/external/github/callback",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IExternalAuthProvider>();
                services.AddSingleton<IExternalAuthProvider>(new FakeGitHubProvider());
                services.AddSingleton<IExternalAuthProvider>(new FakeFeishuProvider());
            });
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@example.com",
            password = "Admin@12345",
        });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var adminToken = loginDoc.RootElement.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        using var bindRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/external/Feishu/bind/start");
        bindRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        bindRequest.Content = JsonContent.Create(new { returnUrl = "/admin/security" });
        var bindStart = await client.SendAsync(bindRequest);
        Assert.Equal(HttpStatusCode.OK, bindStart.StatusCode);

        using var bindDoc = JsonDocument.Parse(await bindStart.Content.ReadAsStringAsync());
        var authorizeUrl = bindDoc.RootElement.GetProperty("data").GetProperty("url").GetString();
        Assert.NotNull(authorizeUrl);
        var state = QueryHelpers.ParseQuery(new Uri(authorizeUrl!).Query)["state"].ToString();

        var callback = await client.GetAsync(
            $"/api/auth/external/Feishu/callback?code=fake-code&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("/admin/security?binding=success", callback.Headers.Location!.ToString());

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "admin@example.com");
            Assert.True(await db.ExternalLogins.AnyAsync(x =>
                x.UserId == admin.Id
                && x.Provider == ExternalAuthProvider.Feishu
                && x.ProviderUserId == FakeFeishuProvider.Subject));
        }

        using var unbindRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/external/bindings/Feishu");
        unbindRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var unbind = await client.SendAsync(unbindRequest);
        Assert.Equal(HttpStatusCode.OK, unbind.StatusCode);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "admin@example.com");
            Assert.False(await db.ExternalLogins.AnyAsync(x =>
                x.UserId == admin.Id && x.Provider == ExternalAuthProvider.Feishu));
        }
    }

    private async Task<Guid> EnsureAdminBindingAsync()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var admin = await db.Users.SingleAsync(u => u.Email == "admin@example.com");

        if (!await db.ExternalLogins.AnyAsync(x =>
                x.UserId == admin.Id
                && x.Provider == ExternalAuthProvider.GitHub
                && x.ProviderUserId == FakeGitHubProvider.Subject))
        {
            db.ExternalLogins.Add(new ExternalLogin(admin.Id, ExternalAuthProvider.GitHub, FakeGitHubProvider.Subject));
            await db.SaveChangesAsync();
        }

        return admin.Id;
    }

    private sealed class FakeGitHubProvider : IExternalAuthProvider
    {
        public const string Subject = "fake-github-user";

        public ExternalAuthProvider Provider => ExternalAuthProvider.GitHub;
        public bool IsEnabled => true;
        public bool SupportsQr => false;

        public string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request) =>
            $"https://fake.github/authorize?state={Uri.EscapeDataString(request.State)}";

        public Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExternalIdentity(ExternalAuthProvider.GitHub, Subject));
    }
    private sealed class FakeFeishuProvider : IExternalAuthProvider
    {
        public const string Subject = "fake-feishu-user";

        public ExternalAuthProvider Provider => ExternalAuthProvider.Feishu;
        public bool IsEnabled => true;
        public bool SupportsQr => true;

        public string BuildAuthorizationUrl(ExternalAuthAuthorizationRequest request) =>
            $"https://fake.feishu/authorize?state={Uri.EscapeDataString(request.State)}";

        public Task<ExternalIdentity> ExchangeCodeAsync(
            ExternalAuthCodeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExternalIdentity(ExternalAuthProvider.Feishu, Subject, "tenant-test"));
    }
}
