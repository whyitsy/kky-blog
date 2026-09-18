# 部署相关产物

> 记录日期：2026-09-10（**2026-09-19 重写 §1~§4**：PG 改用社区镜像、删掉自建 Dockerfile）｜ 相关说明见 [../docs/01-快速开始.md](../docs/01-快速开始.md) §5.4

本目录放**部署期**需要的产物（镜像定义、站点配置）。
它不属于任何 .NET 工程，因此不参与 `dotnet build`。

**本地只需要构建两个镜像**：`webapi.Dockerfile`（后端）、`nginx.Dockerfile`（前端 + 网关）。
PostgreSQL 与 Redis 都是**第三方镜像，只拉不建**。

---

## 1. PostgreSQL 镜像：改用社区镜像（不再自建）

### 为什么需要带 zhparser 的镜像

PostgreSQL 内置分词器只按空格/标点切分，对中文会把**整句当成一个词元**，
因此必须安装中文分词器 **`zhparser`**（依赖 **SCWS** 词法库）。
它**不在官方 `postgres` 镜像里**。

### 结论：用 `mixdeve/postgres-zhparser`

```yaml
# docker-compose.yml
pgsql:
  image: ${POSTGRES_IMAGE:-mixdeve/postgres-zhparser:18}
```

| 项 | 值 |
|---|---|
| 镜像 | `mixdeve/postgres-zhparser:18` |
| 上游 | [mnixry/postgres-zhparser](https://github.com/mnixry/postgres-zhparser)（Dockerfile 全程只有约 25 行） |
| 基础镜像 | `postgres:18.6-bookworm`（`PG_VERSION=18.6-1.pgdg12+2`） |
| 内容 | `/usr/local/scws` + zhparser 装进 `pg_config` 的 `pkglibdir` / `sharedir` |
| 支持版本 | PG **12~18**；架构 `linux/amd64`、`linux/arm64` |
| 体积 | 约 157 MB（压缩后） |

### ⚠️ 为什么曾经自建、现在不自建了

本项目原先用 `deploy/postgres-zhparser.Dockerfile` 从源码编译 SCWS + zhparser
（该文件与 `deploy/postgres-init/` 已于 2026-09-19 删除，可 `git log` 查看）。
当时踩的坑（`libscws-dev` 不在 Debian 源、SCWS 仓库脚本名是 `acprep` 而非 `autogen.sh`、
`Makefile.am` 里一行 Tab 缩进的 `#` 注释会让 `acprep` 失败等）都是**自建才有的成本**。

**社区镜像把这些问题都解决了**，于是自建变成纯粹的重复劳动，
还要把 439 MB 的产物单独打包上传服务器。改用它之后：
PG 与 Redis 走同一条路径 —— 写 compose 的 `image:` 行，`docker compose pull` 即可。

### ⚠️ 上游只发布大版本 tag（已知取舍）

上游没有 `18.6` 这类 patch 级 tag，**只有 `12`~`18`**。所以做不到像 `redis:7.4.11-alpine`
那样钉到 patch 位：上游重新构建 `18` 时，本地 / CI / 服务器可能拉到不同 patch 的镜像。
这是 `../docs/06-技术债与待办.md` G4「没有版本号约定」在依赖侧的同一个问题。

要完全锁死就改用 digest：

```yaml
image: mixdeve/postgres-zhparser@sha256:<digest>
```

### 数据卷兼容性（换镜像不会丢数据）

社区镜像的 `PGDATA` 与官方镜像**完全一致**，都是 `/var/lib/postgresql/18/docker`，
entrypoint 也是官方那套 `docker-entrypoint.sh`。所以既有的
`pgdata:/var/lib/postgresql` 挂载**原样可用**，换镜像时数据目录不会被重新初始化。

---

## 2. ⚠️ 镜像自带的 `chinese` 配置缺 `j,q` —— 由 EF 迁移补齐

### 问题

社区镜像会在数据目录为空时执行它自带的
`/docker-entrypoint-initdb.d/zhparser.sql`：

```sql
CREATE EXTENSION IF NOT EXISTS zhparser;
CREATE TEXT SEARCH CONFIGURATION chinese (PARSER = zhparser);
ALTER TEXT SEARCH CONFIGURATION chinese ADD MAPPING FOR n,v,a,i,e,l WITH simple;
```

注意最后一行只有 **`n,v,a,i,e,l`**，比本项目需要的少了 **`j`（简称）和 `q`（量词）**。

而 `20260910205225_AddAuthCollectionsAndFts` 里创建配置的那段 SQL 是
**「配置不存在才创建」**（`IF NOT EXISTS (… cfgname = 'chinese')`）——
镜像脚本一旦先跑，那段就整体跳过，于是 `j,q` 永远补不上，**而且不报任何错**。

### 后果（实测，不是推测）

没有映射的 token 类型会被 `to_tsvector` **直接丢弃**、不进索引：

```sql
-- 缺 j,q 时：量词「篇」「个」消失
SELECT to_tsvector('chinese','一篇文章 五个参数');
--  '参数':2 '文章':1
-- 补上 j,q 后：
--  '个':3 '参数':4 '文章':2 '篇':1
```

### 解法：迁移 `20260918174440_EnsureChineseConfigTokenMappings`

| 做什么 | 为什么 |
|---|---|
| `ADD MAPPING FOR j,q`（DO 块自己判存在性） | PostgreSQL **没有** `ADD MAPPING IF NOT EXISTS` 语法（实测 PG18 报 `42601`） |
| 补完映射后重写一次 `Posts` | `SearchVector` 是**生成列**，只在行写入时计算；改 `pg_ts_config_map` 不会让 PostgreSQL 重算任何生成列 |
| 重写用 `SET "Title"="Title", "Summary"="Summary", "Content"="Content"` | 必须 SET **生成表达式真正引用的列**。⚠️ 写成 `SET "Id"="Id"`（主键）**不会**触发重算 —— 实测语句报 `UPDATE 1`，但 `SearchVector` 原封不动。这个错误**完全静默** |
| 只在「本次真的补了映射」时才重写 | 全新库（迁移先于任何文章）不写任何行；已带 `j,q` 的库不进重写分支 |

> **为什么不能改老迁移**：EF 按迁移 ID 记录已应用的迁移，**对已上线的库改老迁移内容
> 不会有任何效果**（它已被跳过）。所以必须新增一条。

### 因此不再挂载 `deploy/postgres-init/`

`deploy/postgres-init/01-zhparser.sql` 已删除。理由：
**配置的权威来源必须只有一个**。若同时保留"挂载脚本"和"迁移"两条路径，
将来改映射时很容易只改一处，而另一处静默地继续保持旧行为 ——
这正是本节记录的故障模式本身。现在统一由 EF 迁移负责。

**顺带的好处**：`pg_ts_config` 是**按库**存在的（不是按实例）。在已有实例上
`CREATE DATABASE` 建出的新库里**没有** `chinese` 配置 —— 挂载脚本帮不上忙，
而迁移每次都会在该库上执行，所以"迁移是唯一权威"才是自洽的做法。

### CI 里有一道回归防护

`.github/workflows/ci.yml` 的「校验 chinese 检索配置的映射完整」步骤会在跑完测试后
断言映射恰好是 `a,e,i,j,l,n,q,v`，并确认量词确实进了索引。
**放在测试之后**是必须的：迁移由应用启动时执行，提前断言会假红。

> 映射缺项是**完全静默**的故障：SQL 不报错，`/health` 也发现不了
> （健康检查只查「zhparser 扩展在不在」和「`chinese` 配置存不存在」）。
> 所以它值得一条专门的流水线断言。

---

## 3. 验证：中文分词确实可用

### 3.1 快速验证（开发容器）

```bash
docker exec -it pgsql psql -U kky -d blog_stage2 -c \
  "SELECT to_tsvector('chinese', '使用 EF Core 做数据库优化与全文检索');"
```

预期是**词级切分**：

```
'core':3 'ef':2 '优化':6 '使用':1 '做':4 '全文检索':7 '数据库':5
```

### 3.2 验证映射完整（缺项是静默的，所以要显式查）

```bash
docker exec pgsql psql -U kky -d blog_stage2 -tAc "
SELECT string_agg(DISTINCT t.alias, ',' ORDER BY t.alias)
FROM pg_ts_config c
JOIN pg_ts_config_map m ON m.mapcfg = c.oid
JOIN ts_token_type(c.cfgparser) t ON t.tokid = m.maptokentype
WHERE c.cfgname = 'chinese';"
# 期望：a,e,i,j,l,n,q,v
```

### 3.3 日志里的正常噪音

容器日志可能出现 `custom dict ... not loaded (missing or unreadable)` ——
这是 zhparser 在找**自定义词典**（用于加专业词汇），未提供时属正常，不影响分词。

---

## 4. 换 PG 镜像的操作步骤（数据卷沿用，不丢数据）

> 数据在**命名卷**里（而不是容器内），所以**替换容器不会丢数据**。

```bash
# 1) 先确认数据卷名（下面这条会打印卷名，形如 3aaacea5...）
docker inspect pgsql --format '{{range .Mounts}}{{.Name}}{{end}}'

# 2) 停掉旧容器但保留它（万一要回退）
docker stop pgsql && docker rename pgsql pgsql-old

# 3) 用社区镜像启动新容器，挂同一个数据卷
docker run -d --name pgsql \
  -e POSTGRES_USER=kky -e POSTGRES_PASSWORD=123456 -e POSTGRES_DB=blog_stage2 \
  -p 5432:5432 \
  -v <上一步打印的卷名>:/var/lib/postgresql \
  mixdeve/postgres-zhparser:18

# 4) 验证既有库仍在，并确认扩展、检索配置与映射
docker exec pgsql psql -U kky -d blog_stage2 -tAc \
  "SELECT extname FROM pg_extension WHERE extname='zhparser';"
docker exec pgsql psql -U kky -d blog_stage2 -tAc \
  "SELECT to_tsvector('chinese','使用 EF Core 做数据库优化与全文检索');"

# 5) 确认无误后删除旧容器
docker rm pgsql-old
```

> **注意**：`-v <卷名>:/var/lib/postgresql` 的挂载点是 `/var/lib/postgresql`，
> 与官方镜像一致（不是 `/var/lib/postgresql/data`）。PG18 的 `PGDATA` 实际是
> `/var/lib/postgresql/18/docker`，所以卷挂在这一层才能覆盖到数据目录。
> 写错会导致容器以为数据目录为空而重新初始化，表现为「数据看起来丢了」（实际还在卷里）。

> **用 compose 的场合**不必手敲上面这些：改 `docker-compose.yml` 的 `image:` 行，
> 然后 `docker compose up -d pgsql` 即可（卷由 compose 管理，自动沿用）。
> `j,q` 映射缺失由应用启动时的迁移补齐，不需要人工跑 SQL。

### 改用社区镜像的实测记录（2026-09-19）

在**独立端口的一次性容器 + 全新空数据卷**上验证：

| 检查项 | 结果 |
|---|---|
| 镜像可拉取并启动 | ✅ `mixdeve/postgres-zhparser:18`，约 157 MB |
| 基础版本 | ✅ `PG_VERSION=18.6-1.pgdg12+2`（与原自建镜像的 `postgres:18.6` 一致） |
| `PGDATA` | ✅ `/var/lib/postgresql/18/docker`，与原挂载点兼容 |
| 镜像自带脚本建出的映射 | ✅（也正是问题所在）`a,e,i,l,n,v` |
| 迁移后映射 | ✅ `a,e,i,j,l,n,q,v` |
| 中文分词为词级 | ✅ `'core':3 'ef':2 '优化':6 '使用':1 '做':4 '全文检索':7 '数据库':5` |
| 全新库跑完整迁移链 | ✅ 5 条迁移全部成功，且资料检索可命中「篇」 |
| 半配置 + 存量文章的库 | ✅ 迁移补映射并重算 `SearchVector`，存量行可被搜到 |
| 重复执行迁移 | ✅ 幂等，且**不**重复重写 `Posts` |

---

## 5. 应用镜像与本地整栈编排（`docker-compose.yml`）

> 记录日期：2026-09-12 ｜ 相关说明见 [../docs/05-运维与部署手册.md](../docs/05-运维与部署手册.md) §8.4

### 5.1 本目录新增的产物

| 文件 | 职责 |
|---|---|
| `webapi.Dockerfile` | 后端镜像（多阶段：SDK 构建 → ASP.NET 运行时） |
| `nginx.Dockerfile` | 前端构建（Node）→ Nginx 网关镜像 |
| `nginx.conf` | Nginx 站点配置：静态文件 + `/api` 反代 + SPA fallback |
| `../docker-compose.yml` | 四服务编排：`nginx` / `webapi` / `pgsql` / `redis` —— 其中 **pgsql 与 redis 是第三方镜像（只拉不建）**，见 §1 |
| `../.env.example` | 环境变量模板（**含密钥的真实 `.env` 不入库**） |
| `../.dockerignore` | 构建上下文排除规则（**必需**，见 §5.4） |

### 5.2 为什么要在本地跑「生产形态」

`docs/05` §8.2 预告过的**只在部署后才暴露**的坑。用这套 compose，
它们**全部可以在本地复现**，从而在买服务器之前就踩完：

| 坑 | 本地复现方式 |
|---|---|
| SPA history fallback | `curl -i http://localhost:8080/post/<id>` —— 配错立刻 404 |
| `X-Forwarded-For` | 对比带/不带伪造头的限流行为（§5.5 有实测脚本） |
| 工作目录 | 容器内 `WORKDIR /app` 决定 `logs/` 与 `media/` 落在哪 |

**收益**：远程部署时你只需要面对「服务器环境」这一个变量，而不是两个。

### 5.3 用法

```bash
# 在仓库根目录
cp .env.example .env                    # 填好 JWT_SIGNING_KEY 与 POSTGRES_PASSWORD
docker compose up -d --build            # 首次构建较慢（拉 SDK/Node 镜像）
docker compose ps                       # 四个服务都应为 healthy
curl -i http://localhost:8080/health    # 期望 200 + Healthy
docker compose logs -f webapi           # 看应用日志
docker compose down                     # 停止（数据在命名卷里，不会丢）
docker compose down -v                  # ⚠️ 连数据卷一起删
```

> 端口只发布 `nginx` 的 `8080`。`pgsql` 与 `redis` **刻意不映射到宿主机**：
> ① 避免与开发用的独立容器 `pgsql`/`redis` 抢占 5432/6379；
> ② 更接近生产——数据库不该直接对外。
> 需要直连时用 `docker compose exec pgsql psql -U kky -d blog_stage2`。

### 5.4 ⚠️ 构建踩到的坑（两个都是真实发生过的）

**坑 A：缺少 `.dockerignore` 会让 Windows 的 `obj/` 污染 Linux 构建**

`COPY Blog.Backend/ ./` 会把宿主机上 **Windows 生成的 `obj/`** 一起复制进镜像，
其中的 `project.assets.json` 记录着 Windows 路径。Linux 容器里 MSBuild 读它直接报错：

```
error MSB4018: The "ResolvePackageAssets" task failed unexpectedly.
NuGet.Packaging.Core.PackagingException:
  Unable to find fallback package folder
  'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'
```

**注意它的顺序**：`dotnet restore` 先在容器里跑成功了，是**随后的 `COPY` 把正确结果覆盖掉了**。
这类"文件顺序导致"的失败最难从报错本身看出来。

**解法**：仓库根的 `.dockerignore` 里排除 `**/bin` 与 `**/obj`（已落地）。

**坑 B：Docker Hub 在部分网络下不可达**

`docker compose up` 报 `failed to resolve reference "docker.io/library/redis:<tag>"`。
`mcr.microsoft.com`（.NET 官方镜像）通常可达，但 Docker Hub 不一定。

**解法**：给 Docker Desktop 配置 registry mirror（Settings → Docker Engine）：

```json
{
  "registry-mirrors": ["https://docker.m.daocloud.io"]
}
```

配置后需重启 Docker Desktop。**镜像源地址会随时间失效，本文记录的只是一个 2026-09 仍可用的。**

**坑 C：健康检查写 `localhost` 会让容器永远 `unhealthy`，但服务其实完全正常**

最初 nginx 的健康检查写的是 `wget -qO- http://localhost/`，结果：

```
wget: can't connect to remote host: Connection refused
```

而**从宿主机 `curl http://localhost:8080/` 一切正常**。

**根因**：Alpine 里 `localhost` 优先解析成 IPv6 `[::1]`，
而 nginx 默认的 `listen 80;` **只绑 IPv4**。于是容器内探活失败、外部访问正常。

**解法**（两处都改了，缺一不可）：

| 位置 | 改动 |
|---|---|
| `nginx.Dockerfile` / `webapi.Dockerfile` 的 HEALTHCHECK | 地址写 `127.0.0.1`，不写 `localhost` |
| `nginx.conf` | 加 `listen [::]:80;`，同时监听 IPv6 |

> **这个坑的教训**：「服务能访问」和「探针说健康」是**两个独立的信号**。
> 只验证前者，你会带着一个永远 unhealthy 的容器上线 ——
> 而编排系统（Swarm / K8s / 甚至 `depends_on: service_healthy`）会因此拒绝启动下游服务。

### 5.5 验证记录（2026-09-12 实测）

| 检查项 | 结果 |
|---|---|
| 四个容器状态 | ✅ `pgsql` / `redis` / `webapi` / `nginx` **全部 `healthy`** |
| `/health` 经 Nginx 反代 | ✅ `200` + `Healthy` |
| `/api/site/config` 经 Nginx | ✅ `200`，返回真实种子数据 |
| 前端首页 | ✅ `200` / `text/html` / 904 字节 |
| 静态资源缓存头 | ✅ `/assets/*` 返回 `Cache-Control: max-age=31536000` + `public, immutable` |
| **坑 1** SPA fallback | ✅ `/post/<uuid>`、`/admin/posts/new`、`/collections/xxx` 均返回 index.html；`/assets/不存在.js` 正确 404 |
| **坑 2** `X-Forwarded-For` | ✅ 修复后：伪造 XFF 连发 28 次 → 26 次被限流（修复前 **0 次**，见 §5.6） |
| **坑 3** 工作目录与持久化 | ✅ 上传文件写入 `media` 卷（`app` 用户所有）；日志写入 `logs` 卷；`media-seed` 只读回退可用（`GET /api/files/avatar-default.webp` → `200 image/webp`） |
| 新增坑：上传体积 | ✅ 2 MiB 文件上传成功且在**后端日志中可见**（未设 `client_max_body_size` 时会被 nginx 413 拦掉，后端毫无记录） |
| 路径穿越防护 | ✅ `/api/files/....//....//etc/passwd` 到达应用后返回 `404 文件不存在` |

### 5.6 ⚠️ 发现并修复的安全缺陷：限流可被绕过

**这是本轮最有价值的发现，且只有把栈真正跑起来才能发现。**

| 测试 | 修复前 | 修复后 |
|---|---|---|
| A：正常请求 28 次 | 第 21 次起 `429` ✅ | 同左 ✅ |
| B：每次伪造不同的 `X-Forwarded-For` | **0 次 `429`**（完全绕过）❌ | **26 次 `429`** ✅ |

**根因是信任边界搞错了**：

1. `nginx.conf` 原先用 `proxy_add_x_forwarded_for`（nginx 文档里的常见范例），
   它会把**客户端自己发的 `X-Forwarded-For` 保留在前面**再追加真实 IP，
   头变成 `"<客户端伪造的IP>, <真实IP>"`。
2. 而 `RateLimitingMiddleware.GetClientIp()` 取的是 `Split(',')[0]` —— **第一段**，
   也就是**客户端完全可控的那一段**。

于是只要每次请求换一个伪造 IP，每个请求都会拿到一个全新的令牌桶。

**修复**：nginx 侧改用 `$remote_addr` **覆盖**整个头，把不可信输入丢掉：

```nginx
proxy_set_header X-Forwarded-For $remote_addr;
```

**前提**：nginx 是唯一入口（本项目拓扑正是如此，`webapi` 不对外发布端口）。
若将来前面再加 CDN / 云负载均衡，需要改用 `ngx_http_realip_module` 信任上游网段，
**而不能简单回到追加写法**。

> **更彻底的方案**（`[计划中]`，见 [../docs/06-技术债与待办.md](../docs/06-技术债与待办.md) §2 的 G10）：
> 应用侧改用 ASP.NET Core 的 `ForwardedHeadersMiddleware`，
> 通过 `KnownProxies` / `KnownNetworks` 显式声明**只信任哪些代理**发来的转发头。
> 那才是把"信任边界"表达在代码里，而不是依赖部署配置。
> 在当前拓扑下 nginx 覆盖已经足够，故未立即实施。
