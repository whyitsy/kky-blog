namespace Blog.Application.Interfaces
{
    /// <summary>
    /// 构建版本信息（对应环境变量，由 CI 在镜像构建时注入）。
    ///
    /// <para><b>为什么走环境变量而不是「读 .git」</b></para>
    /// 应用是**在 Docker 里构建**的，而构建上下文只 COPY 了 <c>Blog.Backend/</c>
    /// （见 <c>deploy/webapi.Dockerfile</c>）—— <c>.git</c> 根本不在镜像里，
    /// MSBuild 的 <c>SourceRevisionId</c> 拿不到任何东西。
    /// 所以版本与 SHA 只能由 CI 用 <c>--build-arg</c> 传进来，落到 <c>/app/version</c>。
    /// </summary>
    public class BuildInfoOptions
    {
        /// <summary>环境变量名（同时也对应 /app/version 里的键名）</summary>
        public const string VersionVar = "VERSION";
        public const string CommitVar = "GIT_SHA";
        public const string BuiltAtVar = "BUILD_TIME";

        /// <summary>
        /// 版本号，来自 git tag（语义化版本并带 <c>v</c> 前缀，如 <c>v0.1.0</c>）。
        /// 未注入时留空 —— 由 BuildInfoProvider 决定兜底显示成什么。
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>完整 git commit SHA。本地开发为空</summary>
        public string Commit { get; set; } = string.Empty;

        /// <summary>构建时间（ISO 8601，UTC 或本地带偏移）。未注入时取进程启动时间</summary>
        public string BuiltAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// 对外暴露的版本信息（<c>GET /api/version</c> 的 data 部分）。
    ///
    /// ⚠️ **字段集合是经过收敛的，不要"顺手"加东西。**
    /// 这个端点匿名可访问，多一个字段就多一次信息泄露的机会；
    /// 契约由 <c>VersionEndpointTests</c> 钉住（只允许这三个）。
    /// </summary>
    public sealed record BuildInfoDto(string Version, string Commit, string BuiltAt);
}
