using Blog.Application.Interfaces;
using Blog.Application.Services.Auth;
using Blog.Domain.Entities;
using Blog.Infrastructure.Security.ExternalAuth;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Blog.Tests.Unit;

/// <summary>provider 的授权 URL 只做纯字符串构造，适合用单测锁住关键参数。</summary>
public sealed class ExternalAuthProviderUrlTests
{
    [Fact]
    public void GitHub授权Url_包含PKCE且未配置scope时不带scope()
    {
        var options = new ExternalAuthOptions
        {
            GitHub = new GitHubExternalAuthOptions
            {
                Enabled = true,
                ClientId = "client-id",
                ClientSecret = "secret",
                RedirectUri = "http://localhost:8080/api/auth/external/github/callback",
            },
        };
        var provider = new GitHubAuthProvider(options, Mock.Of<IHttpClientFactory>(), NullLogger<GitHubAuthProvider>.Instance);

        var url = provider.BuildAuthorizationUrl(new ExternalAuthAuthorizationRequest(
            "state-1", "challenge-1", ExternalAuthFlow.Redirect));

        Assert.StartsWith("https://github.com/login/oauth/authorize?", url);
        Assert.Contains("client_id=client-id", url);
        Assert.Contains("state=state-1", url);
        Assert.Contains("code_challenge=challenge-1", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.DoesNotContain("scope=", url);
    }

    [Fact]
    public void 飞书网页授权Url_走v3授权页且不带scope()
    {
        var options = new ExternalAuthOptions
        {
            Feishu = new FeishuExternalAuthOptions
            {
                Enabled = true,
                AppId = "cli_app",
                AppSecret = "secret",
                RedirectUri = "http://localhost:8080/api/auth/external/feishu/callback",
            },
        };
        var provider = new FeishuAuthProvider(options, Mock.Of<IHttpClientFactory>(), NullLogger<FeishuAuthProvider>.Instance);

        var url = provider.BuildAuthorizationUrl(new ExternalAuthAuthorizationRequest(
            "state-1", "challenge-1", ExternalAuthFlow.Redirect));

        Assert.StartsWith("https://accounts.feishu.cn/open-apis/authen/v1/authorize?", url);
        Assert.Contains("client_id=cli_app", url);
        Assert.Contains("state=state-1", url);
        Assert.DoesNotContain("scope=", url);
    }

    [Fact]
    public void 飞书扫码授权Url_走旧passport授权页()
    {
        var options = new ExternalAuthOptions
        {
            Feishu = new FeishuExternalAuthOptions
            {
                Enabled = true,
                AppId = "cli_app",
                AppSecret = "secret",
                RedirectUri = "http://localhost:8080/api/auth/external/feishu/callback",
            },
        };
        var provider = new FeishuAuthProvider(options, Mock.Of<IHttpClientFactory>(), NullLogger<FeishuAuthProvider>.Instance);

        var url = provider.BuildAuthorizationUrl(new ExternalAuthAuthorizationRequest(
            "state-1", null, ExternalAuthFlow.Qr));

        Assert.StartsWith("https://passport.feishu.cn/suite/passport/oauth/authorize?", url);
        Assert.Contains("client_id=cli_app", url);
        Assert.Contains("state=state-1", url);
        Assert.DoesNotContain("scope=", url);
    }
}
