using Blog.Application.Interfaces;

namespace Blog.Infrastructure.Diagnostics
{
    /// <summary>
    /// 读取构建版本信息：由 CI 注入的环境变量（容器里来自
    /// <c>deploy/webapi.Dockerfile</c> 的 <c>ENV</c>，见 <c>--build-arg</c>），
    /// 取不到时给可读的兜底值。
    ///
    /// <para><b>为什么是「读配置 + 兜底」这么简单的一层</b></para>
    /// 这个端点的使用场景是「线上排障」，恰恰是最不能出错的时候：
    ///   - 生产容器：环境变量由 Dockerfile 写入 ✅
    ///   - 本地 <c>dotnet run</c>：没有环境变量 → 给可读兜底，端点仍可用 ✅
    /// 任何一层取不到都不该让端点报错或返回空串（那等于没有这个端点）。
    ///
    /// <para><b>为什么不去读程序集版本</b></para>
    /// 试过，放弃了：<c>Assembly.GetEntryAssembly()</c> 在单元测试宿主里返回的是
    /// **测试运行器**的程序集，不是被测应用 —— 这个兜底在测试里根本验证不了，
    /// 而"验证不了的兜底"等于没有。版本号的唯一权威来源是 CI 注入的环境变量；
    /// 本地开发给个可读的 "unknown" 就够了，不需要伪装成真实版本号。
    /// </summary>
    public sealed class BuildInfoProvider
    {
        /// <summary>未注入版本时的兜底值。刻意用 "unknown" 而不是数字 —— 避免被误认成真实版本</summary>
        public const string UnknownVersion = "unknown";

        private readonly BuildInfoOptions _options;

        public BuildInfoProvider(BuildInfoOptions options)
        {
            _options = options;
        }

        public BuildInfoDto Get()
        {
            // SHA 允许为空：本地构建本来就没有。契约里保留字段，前端/脚本据此判断"未知"。
            var commit = _options.Commit?.Trim() ?? string.Empty;

            // 构建时间缺失时用「当前时间」而不是空串：
            // 空串会让调用方以为解析失败，而"这个进程是什么时候起来的"同样有排障价值。
            var builtAt = string.IsNullOrWhiteSpace(_options.BuiltAt)
                ? DateTimeOffset.UtcNow.ToString("O")
                : _options.BuiltAt.Trim();

            var version = string.IsNullOrWhiteSpace(_options.Version)
                ? UnknownVersion
                : _options.Version.Trim();

            return new BuildInfoDto(version, commit, builtAt);
        }
    }
}
