#!/usr/bin/env bash
# ============================================================================
# 部署唯一入口。完整流程见 tools/deploy/README.md。
#
#   本地（仓库根目录）
#     deploy.sh build [webapi|nginx|all]        构建并打包镜像 → dist-images/
#
#   服务器（部署目录，如 /opt/blog）
#     deploy.sh init   <镜像包.tar.gz> <域名> [邮箱]   首装：swap → .env → 起栈 → HTTPS
#     deploy.sh update <完整 commit sha> [--check]    日常更新：从 GHCR 拉该提交的镜像
#     deploy.sh https  <域名> [邮箱]                  配置/更换 HTTPS（Caddy）
#     deploy.sh status                                现状：容器 / 健康 / 线上版本
#
# 约定：镜像 tag 就是 commit sha（不可变），CI 推 GHCR；本地打包只在首装用。
# ============================================================================
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/../.." && pwd)"
STAMP="$(date +%Y%m%d-%H%M%S)"
DIST="$REPO_ROOT/dist-images"

die()  { printf '\033[31m✗ %s\033[0m\n' "$1" >&2; exit 1; }
step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }
ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; }

SUDO=""
if [ "$(id -u)" -ne 0 ]; then SUDO="sudo"; fi

# ── 小工具 ──────────────────────────────────────────────────────────────────

# 读 .env 的值。刻意不 source：.env 是数据，不是可执行脚本。
env_get() {
  [ -f .env ] || return 0
  grep -E "^$1=" .env | tail -1 | cut -d= -f2- | tr -d '"'"'"' ' || true
}

# 写 .env：有则替换，无则追加
env_set() {
  if grep -qE "^$1=" .env; then
    sed -i "s|^$1=.*|$1=$2|" .env
  else
    printf '%s=%s\n' "$1" "$2" >> .env
  fi
}

# 健康检查要用的端口：HTTP_PORT 可能写成 127.0.0.1:8080 或 8080
listen_port() {
  local hp; hp="$(env_get HTTP_PORT)"; [ -n "$hp" ] || hp=8080
  printf '%s' "${hp##*:}"
}

# 服务器上用哪套 compose：已经写过 GHCR 镜像地址就用 prod 覆盖，否则用基础文件（本地镜像）
use_compose() {
  if [ -f docker-compose.prod.yml ] && [ -n "$(env_get WEBAPI_IMAGE)" ]; then
    export WEBAPI_IMAGE="$(env_get WEBAPI_IMAGE)" NGINX_IMAGE="$(env_get NGINX_IMAGE)"
    COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)
  else
    COMPOSE=(docker compose)
  fi
}

wait_health() {
  local url="http://127.0.0.1:$(listen_port)/health" i
  step "等后端就绪（$url，最多 120 秒）"
  for i in $(seq 1 40); do
    if curl -fsS "$url" >/dev/null 2>&1; then ok "健康检查通过"; return 0; fi
    sleep 3
  done
  "${COMPOSE[@]}" logs --tail=40 webapi || true
  die "后端 120 秒内没有就绪"
}

# 断言容器**实际**在跑哪个镜像：只看"命令跑完了"会被 compose 的"配置没变不重建"骗过去
assert_image() {
  local svc="$1" want="$2" cid got
  cid="$("${COMPOSE[@]}" ps -q "$svc")"
  [ -n "$cid" ] || die "找不到 $svc 容器"
  got="$(docker inspect -f '{{.Config.Image}}' "$cid")"
  [ "$got" = "$want" ] || die "$svc 实际跑的是 $got，期望 $want"
  ok "$svc → $got"
}

# ── build：本地构建并打包 ───────────────────────────────────────────────────

cmd_build() {
  local what="${1:-apps}"
  case "$what" in
    apps|all|webapi|nginx) ;;
    *) die "用法：deploy.sh build [webapi|nginx|all]（默认应用两个镜像）" ;;
  esac
  cd "$REPO_ROOT"
  command -v docker >/dev/null || die "找不到 docker"

  local services=(webapi nginx)
  if [ "$what" = webapi ]; then services=(webapi); fi
  if [ "$what" = nginx ]; then services=(nginx); fi

  step "构建镜像：${services[*]}"
  docker compose build "${services[@]}"

  local save=()
  for s in "${services[@]}"; do
    docker tag "blog-$s:local" "blog-$s:$STAMP"   # 时间戳 tag：本地回滚用
    save+=("blog-$s:local" "blog-$s:$STAMP")
  done

  # PG / Redis 是第三方镜像，只拉不建；--all 时一并打进包，让服务器不必连 Docker Hub
  if [ "$what" = all ]; then
    step "拉取第三方镜像（pgsql / redis）"
    docker compose pull pgsql redis
    local third=()
    mapfile -t third < <(docker compose config --images | grep -vE '^blog-(webapi|nginx):local$' || true)
    save+=(${third[@]+"${third[@]}"})
  fi

  mkdir -p "$DIST"
  local tar="$DIST/blog-images-$STAMP.tar.gz"
  step "打包 → $tar"
  docker save "${save[@]}" | gzip > "$tar"
  local size
  size="$(stat -c%s "$tar" 2>/dev/null || stat -f%z "$tar")"
  [ "$size" -ge 1048576 ] || die "出包只有 $size 字节，判定为失败"
  ok "$(du -h "$tar" | cut -f1)"

  printf '\n首次部署：把下面这些传到服务器，再跑 deploy.sh init\n'
  printf '  scp %s docker-compose.yml docker-compose.prod.yml <用户>@<服务器>:/opt/blog/\n' "$tar"
  printf '  ssh <用户>@<服务器> "mkdir -p /opt/blog/tools/deploy"\n'
  printf '  scp tools/deploy/deploy.sh <用户>@<服务器>:/opt/blog/tools/deploy/\n'
  printf '  ⚠️ 脚本必须落在 /opt/blog/tools/deploy/ 下：CI 的自动部署（main / v* tag）\n'
  printf '     就是执行那里的这一份，路径写错会让自动部署找不到脚本。\n'
}

# ── init：服务器首装 ────────────────────────────────────────────────────────

setup_swap() {
  local mem swap
  mem=$(awk '/^MemTotal:/{printf "%d", $2/1024}' /proc/meminfo)
  swap=$(awk '/^SwapTotal:/{printf "%d", $2/1024}' /proc/meminfo)
  if [ "$mem" -gt 3072 ] || [ "$swap" -ge 1024 ]; then
    ok "内存 ${mem} MB / swap ${swap} MB，无需处理"
    return 0
  fi
  step "创建 2 GB swap（2G 机器上没有 swap，PG 会被 OOM Kill）"
  if [ ! -f /swapfile ]; then
    $SUDO fallocate -l 2G /swapfile 2>/dev/null || $SUDO dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
  fi
  $SUDO chmod 600 /swapfile
  $SUDO mkswap /swapfile >/dev/null
  $SUDO swapon /swapfile 2>/dev/null || true
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' | $SUDO tee -a /etc/fstab >/dev/null
  if [ "$(cat /proc/sys/vm/swappiness)" != 10 ]; then
    echo 'vm.swappiness=10' | $SUDO tee /etc/sysctl.d/99-blog.conf >/dev/null
    $SUDO sysctl -p /etc/sysctl.d/99-blog.conf >/dev/null
  fi
  ok "swap $(awk '/^SwapTotal:/{printf "%d MB", $2/1024}' /proc/meminfo)（已写 fstab，重启仍生效）"
}

write_env() {
  if [ -f .env ]; then ok ".env 已存在，保留原密钥"; return 0; fi
  step "生成 .env"
  local jwt pw tmp
  jwt="$(openssl rand -base64 48 | tr -d '\n')"
  pw="$(openssl rand -base64 24 | tr -d '\n/+')"
  # 密钥为空时立刻失败：空密钥会一路装到 compose 才报错，那时 .env 已经落地了
  [ -n "$jwt" ] && [ -n "$pw" ] || die "openssl 生成密钥失败"
  tmp="$(mktemp)"
  (
    umask 077
    cat > "$tmp" <<EOF
# 由 tools/deploy/deploy.sh init 生成：含真实密钥，不要入库、不要贴聊天
JWT_SIGNING_KEY=$jwt
POSTGRES_PASSWORD=$pw
POSTGRES_USER=kky
POSTGRES_DB=blog_stage2
JWT_ISSUER=blog-api
JWT_AUDIENCE=blog-frontend
# 只监听回环，对外由宿主机 Caddy 终结 TLS 再转发
HTTP_PORT=127.0.0.1:8080
# GHCR 镜像前缀，update 用它拼镜像名；换 owner/仓库时改这一行
IMAGE_PREFIX=${IMAGE_PREFIX:-ghcr.io/CHANGE_ME/CHANGE_ME}
EOF
  )
  mv "$tmp" .env
  chmod 600 .env
  ok ".env 已生成（权限 $(stat -c%a .env 2>/dev/null || stat -f%Lp .env)）"
}

assert_memory_limit() {
  local cid lim
  cid="$("${COMPOSE[@]}" ps -q webapi)"
  [ -n "$cid" ] || die "找不到 webapi 容器"
  lim="$(docker inspect -f '{{.HostConfig.Memory}}' "$cid")"
  # 配置里写了 ≠ 内核真的限制了：没上限时 .NET 按宿主机总内存算 GC 上限，2G 机器会挤死 PG
  [ "$lim" != "0" ] || die "webapi 没有内存上限（内核实际值 0），检查 docker-compose.yml 的 deploy.resources.limits.memory"
  ok "webapi 内存上限 $(awk -v b="$lim" 'BEGIN{printf "%d MB", b/1024/1024}')"
}

cmd_init() {
  local tar="${1:-}" domain="${2:-}" email="${3:-}"
  [ -n "$tar" ] && [ -n "$domain" ] || die "用法：deploy.sh init <镜像包.tar.gz> <域名> [邮箱]"
  [ -f "$tar" ] || die "找不到镜像包：$tar"
  [ -f docker-compose.yml ] || die "当前目录没有 docker-compose.yml（应在 /opt/blog 下执行）"
  docker compose version >/dev/null 2>&1 || die "docker compose 不可用（需要 v2 插件）"

  setup_swap
  write_env

  step "装载镜像包"
  gunzip -c "$tar" | docker load

  use_compose
  step "启动整栈"
  "${COMPOSE[@]}" up -d
  wait_health
  assert_memory_limit

  cmd_https "$domain" "$email"

  printf '\n接下来两件事：\n'
  printf '  1. 立刻改掉种子管理员密码（admin@example.com / Admin@12345 是公开的），改完用旧密码再登录一次，必须失败\n'
  printf '  2. 以后更新用：bash tools/deploy/deploy.sh update <CI 摘要里的 commit sha>\n'
}

# ── update：服务器日常更新 ──────────────────────────────────────────────────

cmd_update() {
  local sha="${1:-}" mode="${2:-apply}"
  [ -n "$sha" ] || die "用法：deploy.sh update <完整 commit sha> [--check]"
  case "$mode" in
    apply|--check) ;;
    *) die "第二个参数只支持 --check（收到 '$mode'）" ;;
  esac
  [ -f .env ] || die "当前目录没有 .env（应在 /opt/blog 下执行）"
  [ -f docker-compose.yml ] || die "当前目录没有 docker-compose.yml"
  [ -f docker-compose.prod.yml ] || die "当前目录没有 docker-compose.prod.yml（从 GHCR 更新需要它）"

  sha="${sha#:}"; sha="${sha#sha-}"
  local prefix; prefix="$(env_get IMAGE_PREFIX)"
  case "$prefix" in
    ""|*CHANGE_ME*) die ".env 里的 IMAGE_PREFIX 还没填（形如 ghcr.io/<owner>/<repo>）" ;;
  esac
  export WEBAPI_IMAGE="$prefix-webapi:$sha" NGINX_IMAGE="$prefix-nginx:$sha"
  COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)

  step "从 GHCR 拉取 $sha"
  "${COMPOSE[@]}" pull webapi nginx
  if [ "$mode" = --check ]; then ok "拉取成功（--check 不再往后走）"; return 0; fi

  # 先 pull 成功再改 .env：拉取失败时栈还停在旧版本
  env_set WEBAPI_IMAGE "$WEBAPI_IMAGE"
  env_set NGINX_IMAGE "$NGINX_IMAGE"

  step "切换容器"
  "${COMPOSE[@]}" up -d webapi nginx
  "${COMPOSE[@]}" restart nginx   # nginx 启动时缓存了上游 IP，webapi 重建后不重启会 502
  wait_health
  assert_image webapi "$WEBAPI_IMAGE"
  assert_image nginx "$NGINX_IMAGE"
  ok "线上版本：$(curl -fsS "http://127.0.0.1:$(listen_port)/api/version" || echo '(取不到)')"
}

# ── https：宿主机 Caddy 终结 TLS ────────────────────────────────────────────

cmd_https() {
  local domain="${1:-}" email="${2:-}"
  [ -n "$domain" ] || die "用法：deploy.sh https <域名> [邮箱]"
  local port; port="$(listen_port)"
  step "配置 HTTPS：$domain → 127.0.0.1:$port"

  if ! command -v caddy >/dev/null 2>&1; then
    step "安装 Caddy"
    $SUDO apt-get update -qq
    $SUDO apt-get install -y -qq debian-keyring debian-archive-keyring apt-transport-https curl
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
      | $SUDO gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
      | $SUDO tee /etc/apt/sources.list.d/caddy-stable.list >/dev/null
    $SUDO apt-get update -qq
    $SUDO apt-get install -y -qq caddy
  fi

  $SUDO mkdir -p /etc/caddy
  if [ -n "$email" ]; then
    $SUDO tee /etc/caddy/Caddyfile >/dev/null <<EOF
{
	email $email
}

$domain {
	reverse_proxy 127.0.0.1:$port
}
EOF
  else
    $SUDO tee /etc/caddy/Caddyfile >/dev/null <<EOF
$domain {
	reverse_proxy 127.0.0.1:$port
}
EOF
  fi

  $SUDO caddy validate --config /etc/caddy/Caddyfile >/dev/null
  $SUDO systemctl reload caddy 2>/dev/null || $SUDO systemctl restart caddy
  ok "配置已生效（证书首次申请约 10~30 秒：journalctl -u caddy -f 看 certificate obtained successfully）"
  printf '    前提：%s 的 A 记录已指向本机，且防火墙/安全组放行 80 与 443\n' "$domain"
  printf '    注意：容器 nginx 只监听回环，反代之后"每 IP 限流"依赖 deploy/nginx.conf 的 realip 配置（改过它要重新构建 nginx 镜像）\n'
}

# ── status ──────────────────────────────────────────────────────────────────

cmd_status() {
  [ -f docker-compose.yml ] || die "当前目录没有 docker-compose.yml"
  use_compose
  "${COMPOSE[@]}" ps
  local port; port="$(listen_port)"
  printf '\n/health      %s\n' "$(curl -fsS "http://127.0.0.1:$port/health" 2>/dev/null || echo '不可用')"
  printf '/api/version %s\n' "$(curl -fsS "http://127.0.0.1:$port/api/version" 2>/dev/null || echo '不可用')"
}

# ── 入口 ────────────────────────────────────────────────────────────────────

case "${1:-help}" in
  build)  shift; cmd_build  "$@" ;;
  init)   shift; cmd_init   "$@" ;;
  update) shift; cmd_update "$@" ;;
  https)  shift; cmd_https  "$@" ;;
  status) shift; cmd_status "$@" ;;
  help|-h|--help)
    sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    ;;
  *) die "未知命令：$1（可用：build / init / update / https / status / help）" ;;
esac
