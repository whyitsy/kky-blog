using System.Text;
using System.Text.Json;
using Blog.Application;
using Blog.Application.Common;
using Blog.Application.Interfaces;
using Blog.Domain.Entities;
using Blog.Infrastructure;
using Blog.Infrastructure.Persistence;
using Blog.Infrastructure.Security;
using Blog.WebApi.Authorization;
using Blog.WebApi.HealthChecks;
using Blog.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

// Serilog 启动早期引导日志（应用构建前的异常也能记录）
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog：控制台 + 滚动文件（logs/blog-.log，按天滚动保留 30 天）
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine("logs", "blog-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers();

    // ---------------------------------------------------------------- 统一错误响应
    // [ApiController] 会在 Action 执行**之前**做模型校验，失败时直接短路返回 400。
    // 它默认输出 RFC 7807 ProblemDetails（{type,title,status,errors,traceId}），
    // 与项目对外承诺的 {code,message,data} 是**两套结构**。
    //
    // 后果不是"不好看"，而是调用方必须写两套解析逻辑：
    // 前端拦截器按 {code,message} 处理，一旦某次请求在校验阶段就被拦下，
    // 它拿到的是 {title,errors} —— 用户看到的就是空白或 undefined。
    //
    // 实测缺口（docs/02-架构与数据模型.md §4.1）：
    //   GET /api/posts/search 不带 keyword
    //     → {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1",
    //        "title":"One or more validation errors occurred.", "status":400, ...}
    //
    // 这里接管这个工厂，让"统一响应体"从**多数情况成立**变成**无条件成立**。
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState
                .FirstOrDefault(entry => entry.Value?.Errors.Count > 0);

            // 字段名为 "" 或 "$" 表示错误落在整个请求体上（如 JSON 格式不合法），
            // 这时说「参数 X 不合法」会误导 —— 请求里根本没有这个参数。
            var message = firstError.Key switch
            {
                null or "" or "$" => "请求体格式不正确",
                var field => $"参数「{field}」不合法或缺失",
            };

            return new BadRequestObjectResult(
                ApiResponse.Fail(ErrorCodes.InvalidArgument, message));
        };
    });

    builder.Services.AddOpenApi();

    // ---------------------------------------------------------------- 认证（JWT）
    // 只发 Access Token，不做 Refresh Token（T7）。有效期见 appsettings 的 Jwt 节。
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    // 环境变量用 .NET 标准的分层写法：Jwt__SigningKey（双下划线代表冒号）
    var signingKey = jwtOptions.SigningKey;
    if (string.IsNullOrWhiteSpace(signingKey))
        signingKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;

    if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    {
        // 未配置签名密钥时**直接中止启动**：静默使用弱密钥意味着「任何人都能伪造 token」，
        // 后果比启动失败严重得多。
        throw new InvalidOperationException(
            "未配置 Jwt:SigningKey（或长度不足 32 字节）。签名密钥是敏感信息，不能写进 appsettings.json。\n" +
            "开发期用用户机密或环境变量提供，例如：\n" +
            "  dotnet user-secrets set \"Jwt:SigningKey\" \"<至少32字节的随机字符串>\"\n" +
            "  # 或设置环境变量 Jwt__SigningKey（注意是双下划线）\n" +
            "生成示例：openssl rand -base64 48");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true,
                // 默认 5 分钟时钟偏移会让「刚过期」的 token 仍可用，显式收紧
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            // 让 401 / 403 也走统一响应体，与业务错误保持一致
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse(); // 阻止框架写入 401 空响应体的默认行为
                    if (context.Response.HasStarted) return;  // 防御性检查, 如果还没有写入响应体才修改Response

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(
                        ApiResponse.Fail(ErrorCodes.Unauthorized, "未认证或登录已过期，请重新登录"),
                        UnifiedJsonOptions.Value));
                },
                OnForbidden = async context =>
                {
                    if (context.Response.HasStarted) return; // 防御性检查, 如果还没有写入响应体才修改Response

                    // 403 还是 401？
                    //
                    // PolicyEvaluator 默认按「用户是否已认证」决定 Forbid / Challenge。
                    // 但本项目的 token 是无状态 JWT：注销 / 改密 / 停用后，旧 token 的签名
                    // 与有效期**依然合法**，框架因此认为「已认证」，授权失败一律走 Forbid。
                    // 而 CurrentUserResolutionMiddleware 已经查明身份为什么不被接受
                    // （账号不存在 / 已停用 / tv 不匹配）—— 那属于**凭据无效**，语义是 401，
                    // 客户端也据此才会清理登录态并跳登录页（问题 6）。
                    //
                    // 没有该标记 = 身份有效但角色/权限不足，才是真正的 403。
                    var credentialRejected = context.HttpContext.Items
                        .ContainsKey(CurrentUserResolutionMiddleware.RejectionKey);

                    if (credentialRejected)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(
                            ApiResponse.Fail(ErrorCodes.Unauthorized, "登录状态已失效，请重新登录"),
                            UnifiedJsonOptions.Value));
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(
                        ApiResponse.Fail(ErrorCodes.Forbidden, "无权限执行该操作"),
                        UnifiedJsonOptions.Value));
                },
            };
        });

    // 授权策略：两档角色（见 docs/02-架构与数据模型.md §10.1）
    //
    // 每个策略都叠加 RequireResolvedUserRequirement：角色来自 JWT claim，
    // 而 JWT 是无状态的（注销/改密后旧 token 在到期前依然签名有效）。
    // 加上这个 requirement 后，「token 已被作废」在授权阶段就是 401，
    // 而不是静默降级成匿名再让各业务分支兜底（问题 6，见 RequireResolvedUser.cs）。
    // 处理器依赖 ICurrentUser（每个请求一份解析结果），因此必须是 Scoped 而不是 Singleton，
    // 否则 DI 校验会拒绝启动：「Cannot consume scoped service from singleton」。
    builder.Services.AddScoped<IAuthorizationHandler, RequireResolvedUserHandler>();

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
            .RequireRole(nameof(UserRole.Admin))
            .AddRequirements(new RequireResolvedUserRequirement()));

        options.AddPolicy(AuthorizationPolicies.ContentWriter, policy => policy
            .RequireRole(nameof(UserRole.Admin), nameof(UserRole.Author))
            .AddRequirements(new RequireResolvedUserRequirement()));

        // 无角色要求的「只要登录」策略（/api/auth/me、/api/auth/logout 用）。
        // 之前它们写裸 [Authorize]，会落到 DefaultPolicy —— 那样就没法稳定地附加
        // 本项目的 requirement（改 DefaultPolicy 会影响所有兜底授权）。
        options.AddPolicy(AuthorizationPolicies.Authenticated, policy =>
            policy.AddRequirements(new RequireResolvedUserRequirement()));
    });

    // 前端开发服务器跨域（Vue3 Vite 默认 5173，可按需扩展）
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy => policy
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // 健康检查：对外只回 Healthy / Unhealthy，详情写日志（见 HealthChecks/HealthCheckSetup.cs）。
    builder.Services.AddBlogHealthChecks();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // 请求日志（含耗时，慢请求一目了然）。
    //
    // 必须注册在 ExceptionHandlingMiddleware **外层**（即先于它注册）。
    // 否则 BusinessException 会先冒泡穿过本中间件，记录下的是异常发生时的 500 与完整堆栈，
    // 而客户端实际收到的是 ExceptionHandlingMiddleware 映射后的 404/403/409 —— 日志与事实不符，
    // 且正常业务失败（重复邮箱、资源不存在等）会刷满 ERR，淹没真正的故障。
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} 响应 {StatusCode} 耗时 {Elapsed:0.0000} ms";

        // 分级：5xx = ERR（真故障），4xx = WRN（业务/权限类可预期失败），其余 = INF
        options.GetLevel = (httpContext, _, ex) =>
            ex is not null ? LogEventLevel.Error
            : httpContext.Response.StatusCode switch
            {
                >= 500 => LogEventLevel.Error,
                >= 400 => LogEventLevel.Warning,
                _ => LogEventLevel.Information,
            };
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // ---------------------------------------------------------------- 路由阶段失败的兜底（问题 1）
    //
    // InvalidModelStateResponseFactory（见上方）只覆盖「进了 Action 之后」的模型校验。
    // 而**路由匹配阶段**的失败更早：{id:guid} 之类的路由约束不匹配时，请求根本到不了
    // Action/中间件，ASP.NET Core 直接写一个**空 body、无 Content-Type** 的 404。
    // 实测（修复前）：GET /api/posts/not-a-guid 与 GET /api/totally/wrong/path
    // 都是 "HTTP 404 | Content-Type='' | Body=<空>"。
    //
    // 于是 docs/02 §4.1 承诺的「所有接口（含错误）返回 {code,message,data}」在路由阶段破功，
    // 前端 http.ts 只能退回「按 HTTP 状态码硬编码兜底」。
    //
    // 这里用状态码页兜底：凡是状态码 ≥ 400 且**响应体为空**的响应，一律补写成统一结构。
    //   - 只在 body 为空时动手 -> 不会覆盖 JwtBearer OnChallenge/OnForbidden、
    //     ExceptionHandlingMiddleware、限流中间件已经写好的统一体
    //   - 刻意**不引入 ProblemDetails**：那会与本项目既有的 ApiResponse 并存成两套结构，
    //     正是这次要消灭的问题
    //   - 位置：必须在 UseRouting 之后（否则没有 Endpoint 可匹配），
    //     且在 UseAuthentication 之前即可 —— 认证失败时 OnChallenge 会自己写 body，
    //     这里因为 body 非空而自动跳过
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var response = statusCodeContext.HttpContext.Response;

        // 已经写过 body 的（业务统一体、JwtBearer 挑战响应、限流响应）一律不碰。
        // 判断依据是 HasStarted —— 写过任何内容都会让响应开始，
        // 比 ContentLength 可靠（框架写入时不一定设置 ContentLength）。
        if (response.HasStarted) return;

        var (code, message) = DescribeHttpError(response.StatusCode);

        response.ContentType = "application/json; charset=utf-8";
        await response.WriteAsync(JsonSerializer.Serialize(
            ApiResponse.Fail(code, message), UnifiedJsonOptions.Value));
    });

    // 令牌桶限流（规则见 appsettings RateLimit 节，Redis 故障自动降级内存桶）
    app.UseMiddleware<RateLimitingMiddleware>();

    app.UseCors("Frontend");

    app.UseAuthentication();

    // 解析并校验登录用户（含 TokenVersion 校验），结果放入 HttpContext.Items 供 ICurrentUser 读取。
    // 放在 UseAuthentication 之后，因为需要先有 ClaimsPrincipal。
    app.UseMiddleware<CurrentUserResolutionMiddleware>();

    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/", () => Results.Ok(new { name = "Blog API", status = "running" }));

    // /health：容器编排与负载均衡用的健康探针（匿名可访问，响应体不含内部细节）
    app.MapBlogHealthChecks();

    // 启动时自动应用迁移（种子数据由 HasData 保证）
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        dbContext.Database.Migrate();
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException 为 EF Core 设计期工具（dotnet-ef）正常中止宿主，不算启动失败
    Log.Fatal(ex, "应用启动失败");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// 把「路由阶段失败」的 HTTP 状态码翻译成统一响应体的 <c>{code,message}</c>（问题 1）。
///
/// 与 <c>ExceptionHandlingMiddleware.MapStatusCode</c>（业务码 → HTTP）方向相反，
/// 这里由框架给的状态码反推业务码。刻意只在少数状态码上做专门处理，
/// 其余统一落到 4001 —— 与业务码映射表「未列出的业务码一律落 400」的兜底精神一致，
/// 不为了好看而新增错误码。
///
/// 文案一律中性、不暴露内部结构（不出现路由模板、端点名、堆栈等）。
/// </summary>
static (int Code, string Message) DescribeHttpError(int statusCode) => statusCode switch
{
    StatusCodes.Status401Unauthorized => (ErrorCodes.Unauthorized, "未认证或登录已过期，请重新登录"),
    StatusCodes.Status403Forbidden => (ErrorCodes.Forbidden, "无权限执行该操作"),
    StatusCodes.Status404NotFound => (ErrorCodes.NotFound, "请求的资源不存在"),
    StatusCodes.Status405MethodNotAllowed => (ErrorCodes.InvalidArgument, "该资源不支持此请求方法"),
    StatusCodes.Status409Conflict => (ErrorCodes.ConcurrencyConflict, "数据已被其他请求修改，请刷新后重试"),
    StatusCodes.Status413PayloadTooLarge => (ErrorCodes.PayloadTooLarge, "请求体过大"),
    StatusCodes.Status429TooManyRequests => (ErrorCodes.RateLimited, "请求过于频繁，请稍后再试"),
    _ => (ErrorCodes.InvalidArgument, "请求无法处理，请检查请求路径与参数"),
};

/// <summary>授权策略名（控制器与 Program 共用，避免魔法字符串不一致）</summary>
internal static class AuthorizationPolicies
{
    /// <summary>仅管理员</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>能写内容的人：管理员或作者</summary>
    public const string ContentWriter = "ContentWriter";

    /// <summary>
    /// 只要「身份有效」即可，不限定角色（/api/auth/me、/api/auth/logout）。
    /// 等价于裸 <c>[Authorize]</c>，但能稳定附加 RequireResolvedUserRequirement。
    /// </summary>
    public const string Authenticated = "Authenticated";
}

/// <summary>统一响应体序列化选项（与 ExceptionHandlingMiddleware 保持一致：camelCase）</summary>
internal static class UnifiedJsonOptions
{
    public static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web);
}

// 集成测试用 WebApplicationFactory<Program> 启动真实宿主，需要顶层语句生成的 Program 可见。
// 用 InternalsVisibleTo 而不是 `public partial class Program;`——后者会**再声明一个空的 Program 类**，
// 与顶层语句生成的 Program 冲突，导致工厂拿到空入口、报 "no web application was configured"。
// 授权见 Blog.WebApi.csproj 的 AssemblyAttribute。

