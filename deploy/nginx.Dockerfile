# syntax=docker/dockerfile:1
#
# 前端 + 网关镜像：用 Nginx 同时提供前端静态文件与 /api 反向代理。

# ────────────────────────────────────────────────────────────────
# 阶段 1：构建前端（Vite）
# ────────────────────────────────────────────────────────────────
#
# 用 bookworm-slim（glibc）而不是 alpine（musl）- 不同的C标准库：
#   本项目依赖里有带**原生二进制**的包（rolldown、lightningcss），
#   npm 按平台安装对应实现。package-lock.json 里 glibc 与 musl 变体都记录了，
#   但 glibc 是最稳妥的默认选择，与开发机（Windows）行为差异最小。
FROM node:24-bookworm-slim AS build
WORKDIR /src

# 与后端 Dockerfile 同理：先复制依赖清单再 npm ci，让依赖层可复用缓存。
# package*.json 这个通配同时匹配 package.json 与 package-lock.json。
COPY Blog.FrontEnd/package.json Blog.FrontEnd/package-lock.json ./

# `npm ci` 而不是 `npm install`：
#   - 严格按 lock 文件安装，构建可复现
#   - lock 与 package.json 不一致时**直接失败**
#   - 不会修改 lock 文件
# 注意不能加 --omit=dev：vue-tsc / vite 都在 devDependencies 里，构建需要它们。
RUN npm ci

COPY Blog.FrontEnd/ ./

RUN npm run build

# ────────────────────────────────────────────────────────────────
# 阶段 2：Nginx
# ────────────────────────────────────────────────────────────────
FROM nginx:1.29-alpine AS runtime

# 官方 nginx 镜像默认会加载 /etc/nginx/conf.d/*.conf。
# 覆盖掉它自带的 default.conf，换成我们的站点配置。
RUN rm -f /etc/nginx/conf.d/default.conf
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf

# 只把 dist/ 搬过来：node_modules 与源码都不进最终镜像
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1/ >/dev/null 2>&1 || exit 1
