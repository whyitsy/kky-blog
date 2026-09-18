using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blog.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 确保 <c>chinese</c> 检索配置带有 <b>j（简称）与 q（量词）</b>两个 token 映射。
    ///
    /// <para><b>为什么需要这条迁移。</b>
    /// PostgreSQL 镜像 <c>mixdeve/postgres-zhparser</c> 自带的
    /// <c>/docker-entrypoint-initdb.d/zhparser.sql</c> 会在数据目录为空时抢先建好
    /// <c>chinese</c> 配置，但它只映射 <c>n,v,a,i,e,l</c>，<b>没有 j 和 q</b>。</para>
    ///
    /// <para>而 <c>20260910205225_AddAuthCollectionsAndFts</c> 里创建配置的那段 SQL 是
    /// <b>「配置不存在才创建」</b>（<c>IF NOT EXISTS (… cfgname = 'chinese')</c>）：
    /// 镜像脚本一旦先跑，那段 SQL 就整体跳过，于是 j、q 永远补不上 ——
    /// <b>而且不报任何错</b>。</para>
    ///
    /// <para><b>后果（实测，不是推测）。</b>没有映射的 token 类型会被
    /// <c>to_tsvector</c> <b>直接丢弃</b>，不进索引：</para>
    ///
    /// <code>
    /// -- 缺 j,q 时：量词「篇」「个」消失
    /// SELECT to_tsvector('chinese','一篇文章 五个参数');
    /// --  '参数':2 '文章':1
    /// -- 补上 j,q 后：
    /// --  '个':3 '参数':4 '文章':2 '篇':1
    /// </code>
    ///
    /// <para><b>为什么不能改那条老迁移。</b>EF 按迁移 ID 记录已应用的迁移，
    /// 对已上线的库修改老迁移内容<b>不会有任何效果</b>（它已被跳过）。
    /// 所以必须新增一条迁移，让「已有库」与「全新库」都能被修到。</para>
    ///
    /// <para><b>为什么还要重算 SearchVector。</b><c>Posts.SearchVector</c> 是
    /// <b>生成列</b>，只在行被写入时计算。改 <c>pg_ts_config_map</c> 属于系统目录变更，
    /// PostgreSQL <b>不会</b>因此重算任何生成列 —— 实测确认：补完映射后旧行仍是旧值，
    /// 必须重写该行才会带上新 token。故本迁移在「确实补了映射」时重写一次 <c>Posts</c>。</para>
    ///
    /// <para>重写用 <c>SET "Title"="Title", "Summary"="Summary", "Content"="Content"</c>
    /// —— 即<b>把生成表达式真正引用的三个源列赋成自身</b>。三者都是原值，
    /// 不改变任何业务数据，但足以让 PostgreSQL 重写整行并重算生成列。</para>
    ///
    /// <para>⚠️ <b>不能图省事写成 SET 主键</b>（<c>SET "Id"="Id"</c>）：
    /// 实测<b>不会</b>触发重算（语句报 UPDATE 1，但 <c>SearchVector</c> 原封不动）。
    /// 只有 SET 生成表达式引用的列才会重算 —— 这是本迁移最容易写错、
    /// 且写错后<b>完全静默</b>（迁移成功、数据没修）的一点。</para>
    ///
    /// <para><b>代价说明。</b>只有「本次真的补了映射」才走重写分支，所以
    /// ① 全新库（迁移先于任何文章执行）不写任何行；
    /// ② 已带 j,q 的库（即本项目此前的自编译镜像）不进重写分支；
    /// ③ 只有「半配置状态下已有文章」的库才会重写一次。</para>
    /// </summary>
    public partial class EnsureChineseConfigTokenMappings : Migration
    {
        /// <summary>
        /// 补映射 + 按需重算，**一个 DO 块搞定**。
        ///
        /// <para>刻意不拆成两步：若拆开，「是否补过映射」这个判断在两步之间会因
        /// 第一步已生效而<b>恒为真</b>，导致每次启动都重写整张 Posts 表。</para>
        ///
        /// <para>探针查 <c>ts_token_type(c.cfgparser)</c> 而不是硬编码 tokid，
        /// 以免依赖 zhparser 的内部编号约定。</para>
        /// </summary>
        private const string EnsureMappingsSql = @"
DO $$
DECLARE
    needs_backfill boolean := false;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_ts_config_map m
        JOIN pg_ts_config c ON c.oid = m.mapcfg
        JOIN ts_token_type(c.cfgparser) t ON t.tokid = m.maptokentype
        WHERE c.cfgname = 'chinese' AND t.alias = 'j'
    ) THEN
        ALTER TEXT SEARCH CONFIGURATION chinese ADD MAPPING FOR j,q WITH simple;
        needs_backfill := true;
        RAISE NOTICE 'chinese 检索配置已补上 j,q 映射，本次将重算 Posts.SearchVector';
    ELSE
        RAISE NOTICE 'chinese 检索配置已含 j,q 映射，跳过重算';
    END IF;

    IF needs_backfill THEN
        UPDATE ""Posts""
        SET ""Title"" = ""Title"",
            ""Summary"" = ""Summary"",
            ""Content"" = ""Content"";
    END IF;
END
$$;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL 没有 `ADD MAPPING IF NOT EXISTS` 语法
            // （实测 PG18 报 42601 语法错误），只能自己在 DO 块里判断。
            migrationBuilder.Sql(EnsureMappingsSql);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 只撤掉本迁移新增的那两个映射，把配置恢复成社区镜像脚本建出的形态
        /// （<c>n,v,a,i,e,l</c>）。
        ///
        /// <para>⚠️ 这是<b>有损回退</b>：<c>j,q</c> 撤掉后量词与简称不再进检索索引。
        /// 这里不做 <c>NotSupportedException</c> —— 本项目其它迁移的 Down 也都是
        /// 尽力而为的真实回退（见 <c>ExtendPostSummaryLength</c> 的备注），保持一致。</para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_ts_config_map m
        JOIN pg_ts_config c ON c.oid = m.mapcfg
        JOIN ts_token_type(c.cfgparser) t ON t.tokid = m.maptokentype
        WHERE c.cfgname = 'chinese' AND t.alias = 'j'
    ) THEN
        ALTER TEXT SEARCH CONFIGURATION chinese DROP MAPPING FOR j,q;
    END IF;
END
$$;");
        }
    }
}
