# 部署 · `tools/deploy/deploy.sh`

一个入口，五个命令。本地 `build`，服务器 `init` / `update` / `https` / `status`。

```
本地（仓库根目录）      deploy.sh build [webapi|nginx|all]
服务器（/opt/blog）     deploy.sh init <镜像包.tar.gz> <域名> [邮箱]
                       deploy.sh update <完整 commit sha> [--check]
                       deploy.sh https <域名> [邮箱]
                       deploy.sh status
```

镜像契约：**tag 就是 commit sha**（不可变，永不使用 `:latest` 部署）。
`ghcr.io/<owner>/<repo>-webapi:<sha>`、`ghcr.io/<owner>/<repo>-nginx:<sha>`。

---

## 1. 端到端流程

```
① 开发
   改代码 ──push──► PR ──► CI 四作业并行（卫生 / 前端 / 后端+测试 / 依赖扫描）
                                │
                          合并进 main
                                ▼
② CI 发布镜像（publish 作业，只在 main 与 v* tag 上跑）
   构建 webapi + nginx ──► 推 GHCR
   tag = <commit-sha>（不可变），另加人读的 v0.0.0-main.<短sha>
   ★ 打 v* tag 时额外移动 :latest
   作业摘要里直接给出下一步命令：bash tools/deploy/deploy.sh update <sha>
                                │
                                ▼
③ 服务器日常更新（几 MB，只拉变化的层）
   deploy.sh update <sha>
     ├─ 从 GHCR 拉这两个镜像
     ├─ 写 .env 的 WEBAPI_IMAGE / NGINX_IMAGE
     ├─ docker compose up -d webapi nginx
     ├─ restart nginx        ← 否则 502（见 §7）
     └─ 校验 /health 与"容器真的在跑这个镜像"

④ 首次部署（一次性，走 ③ 之外的本地打包路径）
   本地   deploy.sh build all            →  dist-images/blog-images-<时间戳>.tar.gz
   scp    tar.gz + docker-compose.yml + docker-compose.prod.yml + tools/deploy/deploy.sh → /opt/blog
   服务器 deploy.sh init <包> <域名>
     ├─ swap（2G 机器必须，自动建 + 写 fstab）
     ├─ 生成 .env（密钥在服务器现生成）
     ├─ docker load → compose up -d → 等 /health
     └─ 装 Caddy + 写 Caddyfile + reload → HTTPS 生效
```

---

## 2. 首次部署

**服务器要求**：Ubuntu 24.04，2 核 2G 起，磁盘 ≥15 GB，能连 GHCR（或 Docker Hub）。
swap 由 `init` 自动创建（2G 内存没有 swap 时，PG 做 VACUUM / 大排序会被内核 OOM Kill），不用手工做。

> 选型时的排序是 **带宽 > 月流量包 > 磁盘 ≥ 内存 > 核数**：同样写"2 核 2G"，
> 不同套餐的带宽能差几十倍，而核数是这里最不重要的指标（实测 p95 只有 2~8 ms）。
> 内地节点用域名必须 ICP 备案，香港节点免备案但延迟 70~120 ms；
> 镜像选 Ubuntu 24.04 LTS（**不要**带面板的应用镜像），并确认机器有快照功能。

```bash
# ① 本地：构建并打包（--all 会把 PG / Redis 镜像也带上，服务器就不用连 Docker Hub）
bash tools/deploy/deploy.sh build all

# ② 上传（只传这几个文件，不传源码、不传 .env）
scp dist-images/blog-images-*.tar.gz docker-compose.yml docker-compose.prod.yml \
    <用户>@<服务器>:/opt/blog/
ssh <用户>@<服务器> 'mkdir -p /opt/blog/tools/deploy'
scp tools/deploy/deploy.sh <用户>@<服务器>:/opt/blog/tools/deploy/

# ③ 服务器：一条命令装完
cd /opt/blog
IMAGE_PREFIX=ghcr.io/<owner>/<repo> bash tools/deploy/deploy.sh init blog-images-<时间戳>.tar.gz blog.example.com
```

`IMAGE_PREFIX` 只有配了它，以后才能用 `update` 走 GHCR；不配也能用，但 `.env` 里是 `CHANGE_ME` 占位，`update` 会直接报错提醒你补。

跑完还有两件事（脚本会再打印一次）：
1. **立刻改掉种子管理员密码**（`admin@example.com / Admin@12345` 是公开的），改完用旧密码再登录一次，必须失败。
2. 站点只监听 `127.0.0.1:8080`，公网访问靠 Caddy（`init` 已经配好）。

**一次性设置**：GHCR 上的 package 默认私有，第一次推完镜像后去
GitHub → Packages → 对应 package → Settings → visibility 改成 **public**（服务器才能免登录拉取）。

---

## 3. 日常更新

```bash
# sha 从 CI 的 publish 作业摘要里抄
bash tools/deploy/deploy.sh update <完整 sha>

# 只拉不切，先确认能拉到（不改 .env、不重建容器）
bash tools/deploy/deploy.sh update <完整 sha> --check
```

只接受**完整** sha：短 sha 在 registry 里没有对应 tag。
失败时 `.env` 不会留下半成品 —— 脚本是"先拉取成功、再改 .env"。

---

## 4. 回滚

镜像 tag 不可变，所以回滚 = 用旧 sha 再部署一次：

```bash
bash tools/deploy/deploy.sh update <上一个 sha>
```

⚠️ **只回滚应用，不回滚数据库结构。** 应用启动时会自动跑 EF 迁移，而旧镜像不会把表结构改回去。
本项目**没有备份功能**（个人项目，刻意不做）：表结构变更只能自己想清楚再上，出问题手工处理。

用本地打包路径部署的，还可以用时间戳 tag 回滚：

```bash
docker images | grep blog-webapi          # 找 blog-webapi:<时间戳>
docker tag blog-webapi:<时间戳> blog-webapi:local
docker compose up -d --force-recreate webapi nginx
```

---

## 5. HTTPS / 换域名

```bash
bash tools/deploy/deploy.sh https blog.example.com [you@example.com]
```

脚本做四件事：装 Caddy（已装则跳过）→ 写 `/etc/caddy/Caddyfile`（域名反代到 `127.0.0.1:8080`）
→ `caddy validate` → `systemctl reload caddy`。证书的申请与续期由 Caddy 自动完成。

前置条件：**域名的 A 记录已经指向这台机器**（Let's Encrypt 要回连验证），
并且防火墙与云厂商安全组都放行 **80 / 443** 两个端口。

> 反代之后容器 nginx 看到的对端会变成宿主机，「每 IP 限流」会退化成全站共用一个桶。
> `deploy/nginx.conf` 里的 `realip` 配置就是修这个的 —— 改过它必须重新构建 nginx 镜像。

---

## 6. 改了东西之后要做什么

GHCR 里只有 **webapi / nginx** 两个镜像。compose 文件、脚本、PG 镜像都不在里面 ——
改了那些却只跑 `update`，会出现「镜像换了、配置没换」，而且**不报错**。

| 改了什么 | 要做什么 |
|---|---|
| `Blog.Backend/**`、`Blog.FrontEnd/**`、`deploy/{webapi,nginx}.Dockerfile`、`deploy/nginx.conf` | 等 CI 绿 → `deploy.sh update <sha>` |
| `docker-compose.yml`、`docker-compose.prod.yml` | 还要 `scp` 到 `/opt/blog/` |
| `docker-compose.yml` 里 `pgsql:` / `redis:` 的 `image:` 行 | 服务器执行 `docker compose pull pgsql redis && docker compose up -d pgsql redis`（数据在命名卷里，不会丢） |
| `tools/deploy/deploy.sh` | 还要 `scp` 到 `/opt/blog/tools/deploy/` |
| `tools/load/**`、`docs/**`、`learn/**` | 服务器不需要任何动作 |

---

## 7. 排错

| 现象 | 原因 |
|---|---|
| 更新后 502 | `update` 已自动 `restart nginx`；若是手工 `docker compose up -d` 重建了 webapi，必须自己重启 nginx —— 它在启动时缓存了上游 IP，不会重新解析 |
| `required variable WEBAPI_IMAGE is missing a value` | compose 缺 `.env` 里那两行；`update` 会自己 export，手工敲 compose 命令时要在 `.env` 里写好 |
| 更新"成功"了但功能没变 | compose 可能因"配置没变"没重建容器。`update` 结尾会 `docker inspect` 断言容器**实际**跑的镜像名，别只看命令退出码 |
| 站点突然 502、pgsql 容器重启过 | 2G 机器上没有 swap 被 OOM Kill。`init` 会自动建；手工装的机器用 `swapon --show` 检查 |
| 公网访问不到 | 设计如此：容器只监听 `127.0.0.1:8080`，对外必须经 Caddy（`deploy.sh https <域名>`） |
| 镜像拉不下来 | GHCR package 还是私有（改成 public），或 sha 写短了/不存在（用完整 sha） |
| ⚠️ 不要用 `docker compose up -d`（不带 prod 覆盖）更新 | 会按基础文件把容器换回本机构建的 `blog-webapi:local` —— 看起来成功，实际是旧镜像 |
