using Blog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Blog.Tests.Infrastructure;

/// <summary>
/// 集成测试用的应用工厂。
///
/// 与生产/开发配置的差别只有一处：**把连接串指向独立的测试库**。
/// 其余全部走真实实现（真实 PostgreSQL、真实中间件管道、真实认证授权），
/// 这样测出来的才是有意义的语义——尤其是 [Authorize] 这类只能在完整管道里体现的行为。
///
/// 测试库命名：<c>{原库名}_test_{短随机后缀}</c>。
/// 整个测试运行期间只创建一次、结束时删除，因此不会污染开发库。
/// </summary>
public sealed class BlogApiFactory : WebApplicationFactory<Program>
{
    /// <summary>测试库名（含随机后缀，避免与开发库或其他测试运行冲突）</summary>
    public string TestDatabaseName { get; }

    private readonly string _adminConnectionString;

    /// <summary>原始连接串（指向开发库），仅用于解析出主机/凭据与读取库名</summary>
    private readonly string _sourceConnectionString;

    public BlogApiFactory()
    {
        _sourceConnectionString = ResolveDevelopmentConnectionString();

        var builder = new NpgsqlConnectionStringBuilder(_sourceConnectionString);
        var baseDb = builder.Database ?? "blog_stage2";
        // PostgreSQL 标识符上限 63 字节，这里取 8 位短后缀足够避免碰撞且始终安全
        TestDatabaseName = $"{baseDb}_test_{Guid.NewGuid():N}"[..(baseDb.Length + 14)];

        // 连到 postgres 维护库来建/删测试库
        builder.Database = "postgres";
        _adminConnectionString = builder.ConnectionString;

        CreateTestDatabase();
        // 刻意不做任何「预建 chinese 配置」的准备 —— 见下面那段注释（G12 的回归防护）
        MigrateTestDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 用环境变量来源覆盖连接串：它晚于 appsettings 加载，因此优先级更高。
        // 应用启动时会自动 Migrate()，测试库的表结构与种子数据由此建立。
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = BuildTestConnectionString(),

                // 集成测试会连续发很多请求，远超开发配置的限流阈值
                // （search 规则容量 20、write 规则容量 30），会误报 429 code 4091。
                // 更麻烦的是限流桶存在 **Redis** 里，是跨测试运行共享的外部状态，
                // 会污染后续运行。这里直接关掉：本组测试关注的是权限/并发语义，
                // 限流本身另有专门验证（见 docs 中的 curl 记录）。
                ["RateLimit:Enabled"] = "false",

                // ⚠️ 缓存必须与开发环境**隔离**，否则会出现极难排查的跨运行污染。
                //
                // 为什么必须隔离：测试库每次运行都是新建的 GUID 库（干净），
                // 但**种子账号的 Id 是写死的**，而 Redis 里的缓存 key 由 Id 决定
                // （blog:auth:user:v1:{id}）。于是上一次运行留下的认证快照会被这一次读到：
                // 库里 TokenVersion=1，缓存里却是上一轮被注销测试提升过的版本
                // → 中间件判定 "TokenVersion 不匹配" → 种子 admin 的 token 全部 401。
                //
                // 实测踩过：修复前整轮 220 个用例里有 52 个连环失败，且单跑某个用例却是绿的
                // （因为单跑时 Redis 里恰好没有被污染的 key）。
                //
                // 用随机 db index 隔离：Redis 默认支持 16 个库，测试只碰自己那一个，
                // 既不读开发环境的 key，也不会把开发环境的帖子列表缓存冲掉。
                ["Cache:RedisConnection"] = BuildIsolatedRedisConnection(),
            });
        });
    }

    /// <summary>
    /// 把开发配置里的 Redis 连接串换成一个**随机 db index**，用于测试隔离。
    ///
    /// 关闭 Cache（Provider 改 Memory）也能避免污染，但那样集成测试就再也覆盖不到
    /// Redis 实现本身（缓存降级、哨兵、锁——这些只有跑 Redis 才验证得到），
    /// 所以这里选择「换库」而不是「关掉」。
    /// </summary>
    private static string BuildIsolatedRedisConnection()
    {
        const string fallback = "localhost:6379";

        // 复用 appsettings.Development.json 里的地址，避免测试与开发配置漂移
        string baseConnection;
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blog.Backend.slnx")))
                dir = dir.Parent;

            var settingsPath = Path.Combine(dir!.FullName, "Blog.WebApi", "appsettings.Development.json");
            var config = new ConfigurationBuilder().AddJsonFile(settingsPath).Build();
            baseConnection = config["Cache:RedisConnection"] ?? fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            baseConnection = fallback;
        }

        // 1~15：避开 0（开发环境常用）以及 Redis 集群模式下的保留位
        var index = Random.Shared.Next(1, 16);
        return $"{baseConnection},defaultDatabase={index}";
    }

    private string BuildTestConnectionString()
    {
        var b = new NpgsqlConnectionStringBuilder(_sourceConnectionString) { Database = TestDatabaseName };
        return b.ConnectionString;
    }

    /// <summary>
    /// 在**宿主启动之前**把测试库的表结构与种子数据建好。
    ///
    /// 为什么必须提前建：应用启动时会执行 <c>Database.Migrate()</c>，
    /// 而 **Migrate 不会创建数据库本身**（只建表/应用迁移）。若库是空的，
    /// 启动就会抛异常；WebApplicationFactory 又会把这个异常吞掉，
    /// 只报 "The server has not been started or no web application was configured"，
    /// 非常难排查。所以这里自己先把迁移跑一遍。
    /// </summary>
    private void MigrateTestDatabase()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseNpgsql(BuildTestConnectionString())
            .Options;

        using var db = new BlogDbContext(options);
        db.Database.Migrate();
    }

    // ────────────────────────────────────────────────────────────────────────
    // ⚠️ 这里**刻意没有**「预先创建 chinese 检索配置」这一步（2026-09-13 移除）。
    //
    // 历史上确实需要它：Posts.SearchVector 是**生成列**，表达式用了
    // to_tsvector('chinese', ...)，而迁移链把 AddColumn<SearchVector> 排在了
    // 创建该配置的 SQL **之前**，于是「干净」的库跑迁移必定失败：
    //     42704: text search configuration "chinese" does not exist
    // 当时的应对是在夹具里先把配置建好绕过去 —— 但那等于**把缺陷掩盖在测试里**：
    // 「迁移链能不能从零建库」恰恰是 CI 最该验证的事情之一（缺口 G12）。
    //
    // 现在迁移自己会建扩展与配置（见 20260910205225_AddAuthCollectionsAndFts
    // 最前面的说明），因此这里不再需要任何前置准备 ——
    // **集成测试是在一个完全干净的库上跑迁移的，这本身就是 G12 的回归防护**。
    // 谁再把这个顺序调回去，这里会立刻红，而不是等到换镜像、上生产时才炸。
    // ────────────────────────────────────────────────────────────────────────

    private void CreateTestDatabase()
    {
        using var conn = new NpgsqlConnection(_adminConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // 库名由本类生成（Guid），不含用户输入；这里仍用参数化形式保持一致性
        cmd.CommandText = $"CREATE DATABASE \"{TestDatabaseName}\"";
        cmd.ExecuteNonQuery();
    }

    /// <summary>删除测试库（由测试夹具在全部测试结束后调用）</summary>
    public void DropTestDatabase()
    {
        // 先断开本进程持有的连接，否则 DROP DATABASE 会因连接占用而失败
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            db.Database.CloseConnection();
        }

        NpgsqlConnection.ClearAllPools();

        using var conn = new NpgsqlConnection(_adminConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{TestDatabaseName}\" WITH (FORCE)";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 从 WebApi 的 appsettings.Development.json 读取连接串，
    /// 避免把主机/密码硬编码在测试里（配置改了测试也跟着走）。
    /// </summary>
    private static string ResolveDevelopmentConnectionString()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Blog.Backend.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("未能定位仓库根目录（找不到 Blog.Backend.slnx）");

        var settingsPath = Path.Combine(dir.FullName, "Blog.WebApi", "appsettings.Development.json");
        var config = new ConfigurationBuilder().AddJsonFile(settingsPath).Build();
        return config.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException($"未在 {settingsPath} 中找到 ConnectionStrings:DefaultConnection");
    }
}

/// <summary>
/// 共享夹具：整个测试集合共用一个应用实例（宿主启动一次）。
/// 因为应用启动时会执行迁移，按测试类各起一次会明显变慢。
/// </summary>
public sealed class BlogApiFixture : IDisposable
{
    public BlogApiFactory Factory { get; }

    public BlogApiFixture() => Factory = new BlogApiFactory();

    public HttpClient CreateClient() => Factory.CreateClient();

    public void Dispose()
    {
        try { Factory.DropTestDatabase(); }
        finally { Factory.Dispose(); }
    }
}

/// <summary>把夹具接到 xUnit 的集合上，供多个测试类共享</summary>
[CollectionDefinition(Name)]
public sealed class BlogApiCollection : ICollectionFixture<BlogApiFixture>
{
    public const string Name = "blog-api";
}
