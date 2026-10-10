# kky-blog

个人博客。Vue 3 前端 + ASP.NET Core (.NET 10) 后端 + Docker Compose 单机部署，CI/CD 全在 GitHub Actions 里。

线上：<https://www.kkynet.site>（`GET /api/version` 可以看线上正在跑哪个 commit）

> 这个仓库的重心在**后端与部署**：前端只做到"够用、能打字、能发文章"，
> 后端统一响应体、认证作废、限流、缓存、全文检索，以及部署期的编排/流水线/回滚。 → [docs/项目介绍.md](docs/项目介绍.md)

---

## 1. 技术栈

| 层 | 选型 | 备注 |
|---|---|---|
| 后端 | .NET 10 / ASP.NET Core，Controllers + 中间件 | 分层：Domain / Application / Infrastructure / WebApi |
| ORM | EF Core 10 + Npgsql 10 | 迁移随应用启动自动执行 |
| 数据库 | PostgreSQL 18 + **zhparser**（中文分词） | 社区镜像 `mixdeve/postgres-zhparser:18` |
| 全文检索 | `tsvector` 生成列 + `chinese` 检索配置 | 标题/摘要/正文分级权重 A/B/C |
| 缓存 / 限流 | Redis 7.4.11（`StackExchange.Redis`） | 不可用时降级内存实现，不降级为"没有" |
| 认证 | 自签 JWT（HS256）+ `TokenVersion` | 不做 Refresh Token，目前已够用 |
| 密码 | PBKDF2-HMAC-SHA512 | 框架内置实现，零三方依赖 |
| 图片 | SixLabors.ImageSharp 3.x | 上传转 WebP |
| 日志 | Serilog → 控制台 + 按天滚动文件 | 保留 30 天，挂 `logs` 卷 |
| 前端 | Vue 3 + TypeScript + Vite 8 + Pinia + vue-router | `marked` + `DOMPurify` 渲染 Markdown |
| 网关 | Nginx（容器内） | 静态文件 + `/api` 反代 + SPA fallback |
| TLS | 宿主机 Caddy | 自动申请/续期证书，反代到 `127.0.0.1:8080` |
| 编排 | Docker Compose（`nginx` / `webapi` / `pgsql` / `redis`） | 单机，2 核 2G 起 |
| CI/CD | GitHub Actions → GHCR → SSH 部署 | 六个作业；**只有推 `v*` tag 才碰生产** |

---

## 2. 目录结构

```
.
├── Blog.Backend/                  # .NET 解决方案（.slnx）
│   ├── Blog.Domain/               # 实体 + 仓储接口，不依赖任何其他层
│   ├── Blog.Application/          # 业务服务、DTO、缓存键、字段长度、错误码
│   ├── Blog.Infrastructure/       # EF Core、仓储实现、缓存、限流、安全、文件
│   ├── Blog.WebApi/               # Program.cs、控制器、中间件、健康检查、appsettings
│   ├── Blog.Tests/                # xUnit 单元 + 集成测试（连真实 PG，136 个 [Fact]/[Theory]）
│   ├── Directory.Build.props      # 版本号（正式版本的唯一来源是 CI 注入的 git tag）
│   └── coverage.runsettings       # 覆盖率口径（排除 EF 迁移与源生成代码）
├── Blog.FrontEnd/                 # Vue 3 + Vite（src/ 68 个文件、约 1.1 万行）
│   ├── src/api/                   # 每个后端资源一个 api 模块，统一走 http.ts
│   ├── src/stores/                # Pinia：auth / theme+search（app.ts）/ site
│   ├── src/views/                 # 公开站 + admin/ + me/（作者区）
│   ├── src/utils/markdown.ts      # marked + DOMPurify（XSS 防线在前端这一层）
│   └── 参考设计规范/               # 设计交付；tokens-v2.css 与 src/styles/tokens.css 同源
├── deploy/
│   ├── webapi.Dockerfile          # 后端镜像（多阶段，非 root，HEALTHCHECK 打 /health）
│   ├── nginx.Dockerfile           # 前端构建 → Nginx 镜像
│   ├── nginx.conf                 # 站点配置（静态资源缓存 / XFF 覆盖 / SPA fallback）
│   └── README.md                  # ⭐ PG 中文分词与镜像选型的实测记录
├── tools/
│   ├── deploy/deploy.sh           # 部署唯一入口（build/init/update/https/status）
│   ├── deploy/README.md           # ⭐ 端到端部署流程、回滚、排错
│   └── load/                      # k6 压测脚本 + 造数据 + 性能基线
├── docker-compose.yml             # 本地「生产形态」整栈（可复现三类部署坑）
├── docker-compose.prod.yml        # 服务器覆盖：镜像从 GHCR 拉，不在本机构建
├── .env.example                   # compose 环境变量模板（真实 .env 不入库）
└── .github/workflows/ci.yml       # 六个作业：卫生/前端/后端/安全扫描/发布/部署
```

---

## 3. 本地开发

### 3.1 前置

- .NET SDK 10、Node 24、Docker
- 本地需要 PostgreSQL（带 zhparser）与 Redis。**最省事的做法**是直接起这两个容器：

```bash
docker run -d --name pgsql -p 5432:5432 \
  -e POSTGRES_USER=kky -e POSTGRES_PASSWORD=123456 -e POSTGRES_DB=blog_stage2 \
  mixdeve/postgres-zhparser:18

docker run -d --name redis -p 6379:6379 redis:7.4.11-alpine
```

> 为什么必须是带 zhparser 的镜像：`Posts.SearchVector` 是 `tsvector` **生成列**，
> 表达式里用了 `to_tsvector('chinese', ...)`。官方 `postgres` 镜像没有这个检索配置，
> 迁移会直接失败。细节见 [deploy/README.md](deploy/README.md)。

### 3.2 后端

```bash
cd Blog.Backend
dotnet tool restore                       # 还原 dotnet-ef
dotnet run --project Blog.WebApi          # http://localhost:5131
```

- 启动时会**自动执行 EF 迁移**（`Program.cs` 里的 `Database.Migrate()`），首次启动稍慢。
- 开发环境配置见 `Blog.WebApi/appsettings.Development.json`（含开发用的固定 JWT 密钥）。
- 开发环境**不要**手动配 `Jwt__SigningKey`，那是生产的事。
- 种子管理员是 `admin@example.com / Admin@12345`，**这个密码公开在仓库里**。
  本地开发无所谓，**生产首次部署后必须立刻改掉**（`deploy.sh init` 跑完会再提醒一次）。

### 3.3 前端

```bash
cd Blog.FrontEnd
npm ci
npm run dev                               # http://localhost:5173，/api 代理到 5131
```

`VITE_API_BASE` 留空 → 同源请求走 Vite 代理，**完全绕开 CORS**，相对路径的图片也能直接用。

### 3.4 整栈「生产形态」

```bash
cp .env.example .env     # 填 JWT_SIGNING_KEY（≥32 字节）与 POSTGRES_PASSWORD
docker compose up -d --build
docker compose ps        # 四个服务都应为 healthy
curl -i http://localhost:8080/health
```

只有 `nginx` 发布 `8080`；`pgsql` / `redis` 刻意不映射到宿主机。

---

## 4. 常用命令

```bash
# 后端
dotnet build Blog.Backend/Blog.Backend.slnx -c Release
dotnet test  Blog.Backend/Blog.Tests/Blog.Tests.csproj --settings Blog.Backend/coverage.runsettings
dotnet format Blog.Backend/Blog.Backend.slnx whitespace      # CI 会 verify-no-changes

# 迁移（设计期工厂读 BLOG_CONNECTION，不依赖 Program.cs 的生产校验）
cd Blog.Backend && dotnet ef migrations add <Name> --project Blog.Infrastructure --startup-project Blog.WebApi
BLOG_CONNECTION="Host=localhost;..." dotnet ef database update --project Blog.Infrastructure --startup-project Blog.WebApi

# 前端
npm run build            # vue-tsc -b && vite build

# 部署
bash tools/deploy/deploy.sh build all                    # 本地打包（仅首次部署用）
bash tools/deploy/deploy.sh update <完整sha> [--check]    # 服务器上拉 GHCR 镜像并切换
bash tools/deploy/deploy.sh status
```

---

## 5. 部署一览

拓扑：`公网 → Caddy(443, 宿主机) → nginx(容器, 127.0.0.1:8080) → webapi(8080) → pgsql / redis`

| 场景 | 动作 |
|---|---|
| 首次部署 | 本地 `deploy.sh build all` 打包 → `scp` 三个文件 + 脚本 → 服务器 `deploy.sh init <包> <域名>`（自动建 swap、生成 `.env`、起栈、装 Caddy） |
| 日常上线 | `git tag -a vX.Y.Z && git push origin vX.Y.Z` → CI 自动发布镜像并 SSH 部署 |
| 合并进 main | **只构建并留档镜像，不碰线上** |
| 手工更新 | 服务器 `bash tools/deploy/deploy.sh update <完整sha>` |
| 回滚 | 用上一个 sha 再 `update` 一次（**只回滚应用，不回滚表结构**） |
| HTTPS | `bash tools/deploy/deploy.sh https <域名> [邮箱]` |

首次部署、回滚细节、排错对照表：[tools/deploy/README.md](tools/deploy/README.md)

---

## 6. 安全设计速览

| 威胁 | 措施 | 位置 |
|---|---|---|
| 弱/缺失签名密钥 | 密钥 < 32 字节**直接拒绝启动** | `Program.cs` |
| 注销/改密后旧 token 仍可用 | `User.TokenVersion` 写进 `tv` claim，每请求比对 | `JwtTokenService` / `CurrentUserResolutionMiddleware` |
| 被作废的 token 静默降级成匿名 | 授权策略叠加 `RequireResolvedUserRequirement`，作废即 401 | `RequireResolvedUser.cs` |
| 401/403 语义混淆 | 用 `HttpContext.Items` 的拒绝原因区分「凭据无效」与「权限不足」 | `Program.cs` 的 `OnForbidden` |
| 暴力破解 / 刷接口 | 令牌桶限流（Redis 分布式 + 内存降级），429 + `Retry-After` | `RateLimitingMiddleware` |
| 伪造 `X-Forwarded-For` 绕过限流 | nginx 用 `$remote_addr` **覆盖**该头，不追加 | `deploy/nginx.conf` |
| 密码泄露 | PBKDF2-SHA512 / 21 万次 / 每用户随机盐 / `FixedTimeEquals` | `Pbkdf2PasswordHasher` |
| 目录穿越读任意文件 | 拒绝 `..` 与绝对路径 + 解析后必须仍在根目录下 | `LocalFileStorageService` |
| 存储型 XSS（SVG） | 上传白名单**不含 `.svg`**，Content-Type 表里也没有它 | `LocalFileStorageService` |
| 外链图片/`javascript:` 注入媒体字段 | 媒体地址白名单：只认 `/api/files/` 前缀 + 受限字符集 | `MediaPath.cs` |
| 多实例下限流失效 | 启动期校验：多实例必须用 Redis，否则**拒绝启动** | `DeploymentGuard` |
| 密钥入库 | 真实 `.env` 与 `.dockerignore` 双重排除；`appsettings.Production.json` 不含任何密钥 | `.gitignore` / `.dockerignore` |

统一响应体 `{code, message, data}` 覆盖**所有**出口：业务异常、模型校验、JWT 挑战、
限流、路由 404、未处理异常六条路径，前端只需要一套解析逻辑。

---

## 7. CI 门禁

`.github/workflows/ci.yml` 六个作业：

| 作业 | 做什么 | 何时 |
|---|---|---|
| `hygiene` | 拒绝 CRLF 行尾 | 每次 push / PR |
| `frontend` | `vue-tsc -b --force` + `vite build` | 每次 push / PR |
| `backend` | 格式检查 → 编译 → 全部测试（连**真实** PostgreSQL）→ 迁移链 + 分词映射断言 → 覆盖率 | 每次 push / PR |
| `security` | NuGet（含传递依赖）+ npm 漏洞扫描 | 每次 push / PR，**只报告不阻断** |
| `publish` | 构建两个镜像推 GHCR，tag = commit sha | 合并进 main / 推 `v*` tag |
| `deploy` | 同步 compose 与脚本 → SSH `deploy.sh update` → 从公网核对 sha | **只有推 `v*` tag** |

几处刻意的"防假绿"：测试数为 0 判失败、覆盖率报告没产出判失败、漏洞扫描没输出结论判失败、
部署后必须从公网看到这次的 sha。

---

## 8. 文档地图

| 文档 | 内容 |
|---|---|
| [docs/项目介绍.md](docs/项目介绍.md) | ⭐ 项目介绍：架构、后端设计、安全、部署全流程（偏后端视角） |
| [deploy/README.md](deploy/README.md) | PG 中文分词镜像选型、`chinese` 配置缺 `j,q` 的静默故障、本地整栈验证记录 |
| [tools/deploy/README.md](tools/deploy/README.md) | 部署唯一入口的端到端流程、首次部署、回滚、排错 |
| [tools/load/README.md](tools/load/README.md) | k6 压测、造数据脚本、已测性能基线 |



