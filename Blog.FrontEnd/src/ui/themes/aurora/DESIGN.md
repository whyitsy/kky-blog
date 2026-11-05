# Aurora 主题设计说明（极光霓虹）

> 本文件由原 `参考设计规范/design-system-v2.md` 吸收而来，是 **aurora 主题**的设计说明。
> 通用 Token 契约与主题插件机制见 `docs/主题系统设计.md`；
> 主题实现见 `src/ui/themes/aurora/`。

Aurora 是站点默认主题：暗色优先、霓虹渐变、玻璃拟态、文章详情双栏。
主题 id 为 `aurora`，提供两种配色模式：

| 模式 id | 名称 | scheme |
|---|---|---|
| `dark` | 暗色（默认） | dark |
| `light` | 亮色 | light |

DOM 契约：`<html data-ui-theme="aurora" data-color-mode="dark">`。

---

## 1. 设计原则

- **极光霓虹 Aurora Neon**：保留炫酷的渐变、径向光晕与发光效果，避免过度收敛。
- **暗色优先**：默认暗色；亮色通过切换器显式选择。
- **玻璃拟态 Glassmorphism**：导航使用半透明底色 + 背景模糊 + 微边框。
- **双栏内容**：文章详情采用「正文 760px + 侧栏 300px」布局；归档页为单列时间轴。
- **大字号优先**：正文 17px、辅助说明 15px、说明文字 13px；文章卡片标题 18px
  （最多两行），宁可让页面更长，也不要把正文挤成小字。
- **用宽度填充留白**：首页把容器加到 1600px 而不是放大卡片；1920 屏两侧留白
  从 320px 收到 160px，单卡仍保持在 380px 左右。
- **Token 落地**：颜色、间距、圆角全部由 `tokens.css` 的 Design Token 提供，
  组件不写色值。

---

## 2. 色彩系统

### 2.1 背景色

| Token | Dark | Light | 用途 |
|---|---|---|---|
| `bg-canvas` | `#13141A` | `#F1F3F9` | 页面最底层背景 |
| `bg-surface` | `#1B1D24` | `#FFFFFF` | 卡片、面板背景 |
| `bg-raised` | `#23262F` | `#FFFFFF` | 抬起的按钮/标签 |
| `bg-sunken` | `#0E0F13` | `#E9EDF5` | 下沉区域、代码块 |
| `bg-invert` | `#171A20` | `#F2F5F9` | 反色背景 |

### 2.2 文字色

| Token | Dark | Light | 用途 |
|---|---|---|---|
| `text-strong` | `#F2F5F9` | `#171A20` | 标题、强强调 |
| `text-default` | `#CBDAE5` | `#2E333D` | 正文、主内容 |
| `text-muted` | `#A3A9B8` | `#535B6D` | 次要信息 |
| `text-subtle` | `#8A90A0` | `#6A7284` | 辅助、占位符 |
| `text-disabled` | `#565B68` | `#A2AABE` | 禁用态文字 |
| `text-invert` | `#13141A` | `#FFFFFF` | 反色背景上的文字 |
| `text-on-brand` | `#FFFFFF` | `#FFFFFF` | 渐变/实心品牌底上的文字 |

### 2.3 品牌与强调色

#### Dark 模式
- **brand-500**: `#22C5DC`（青色）
- **sub-500**: `#F4569D`（品红）
- **accent-500**: `#7C5CFF`（紫色）

#### Light 模式
- **brand-500**: `#E93796`（品红）
- **sub-500**: `#547CE7`（蓝色）
- **accent-500**: `#5433FF`（深紫）

### 2.4 极光渐变

- **gradient-start**: `#24C6DC`（青）
- **gradient-mid**: `#5433FF`（紫）
- **gradient-end**: `#FF0099`（品红）

用于：Hero 大标题、Logo、活跃状态、分隔线、分类徽章、主按钮。

### 2.5 边框与状态色

| Token | Dark | Light |
|---|---|---|
| `border-subtle` | `#2A2C36` | `#E6EAF3` |
| `border-default` | `#383B47` | `#D3D9E8` |
| `border-strong` | `#4A4E5C` | `#B9C1D4` |
| `status-success` | `#3DD68C` | `#0F9D58` |
| `status-warning` | `#F5B544` | `#D9800A` |
| `status-danger` | `#F87171` | `#DC2626` |
| `status-info` | `#60A5FA` | `#4166DB` |

---

## 3. 字体系统

- **西文主字体**: `Rubik`（400/500/600/700）
- **中文主字体**: `Noto Sans SC`（400/500/700）
- **代码字体**: `Noto Sans Mono`（400/600）
- **兜底**: `system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`

### 字号层级

| 名称 | 字号 | 行高 | 字重 | 用途 |
|---|---|---|---|---|
| Display | 64px | 1.12 | 700 | 首页 Hero 标题（≤900px 退到 48px，≤640px 退到 38px） |
| H1 | 52px | 1.18 | 700 | 文章详情标题（详情页实际 40px，避免长标题溢出） |
| H2 | 32px | 1.28 | 700 | 章节标题 |
| H3 | 25px | 1.32 | 600 | 小标题 |
| Body | 17px | 1.8 | 400 | 正文 |
| Body-sm | 15px | 1.65 | 400 | 辅助说明 |
| Caption | 13px | 1.55 | 500 | 标签、时间 |

---

## 4. 间距与布局

以 4px 为基准：`space-1=4` … `space-24=96`（完整清单见 foundation/tokens.css）。

- 正文最大宽度：`size-prose = 760px`
- 内容容器：`size-container = 1200px`
- 首页宽容器：`size-container-wide = 1600px`（宽屏两侧留白从 320px 收到 160px）
- 侧栏：`size-sidebar = 300px`
- 顶部导航高度：`nav-height = 64px`（页面内容由 shell 统一偏移）

---

## 5. 圆角系统

| Token | 值 | 用途 |
|---|---|---|
| `radius-xs` | 4px | 小标签、输入框 |
| `radius-sm` | 6px | 按钮、徽章 |
| `radius-md` | 10px | 小卡片 |
| `radius-lg` | 16px | 文章卡片 |
| `radius-xl` | 24px | 大面板、评论卡片 |
| `radius-2xl` | 32px | Hero 搜索框、全圆角胶囊 |

---

## 6. 基础组件规范（foundation primitives + 主题覆盖）

### 6.1 按钮

- **Primary（渐变）**: `linear-gradient` 极光渐变，文字 `text-on-brand`，`radius-sm`，
  hover 轻微上移 + 发光阴影。
- **Secondary（描边）**: 透明底 + `border-default`，文字 `text-default`。
- **Ghost（幽灵）**: 透明底，hover 时背景 `bg-raised`。

### 6.2 卡片

- 背景 `bg-surface`；边框 1px `border-subtle`；圆角 `radius-lg`；
  阴影按层级使用 `shadow-sm` / `shadow-md`。
- 文章卡片封面与正文的衔接参考 `workspace/Blog` 的 ArticleCard：正文整体上移 64px
  压住封面底部，并在自身背景上铺「透明 → `bg-surface`」的竖向渐变，让封面在底部
  自然淡入内容区，文字落在渐变末端的实底上。不做内缩/圆角/落影的独立面板，
  卡片始终是一张连续的面板。

### 6.3 输入框 / 评论框

- 背景 `bg-canvas`；边框 1px `border-default`；占位符 `text-subtle`；
- Focus：`border-brand-500` + `0 0 0 3px color-mix(brand-500 15%)`。

### 6.4 标签 / 徽章

- 标签：半透明品牌底 + 品牌描边（`.tag-badge`）。
- 分类：实心渐变底 + `text-on-brand`（`.category-badge`）。
- 专栏：虚线边框（aurora 文章详情页内覆盖）。
- 其余通用徽章使用 foundation 的 `.badge-*` 语义变体。

### 6.5 导航

- 玻璃拟态固定顶栏：`--glass-bg`（Dark `bg-surface` 82% / Light 白色 82%）+
  `backdrop-blur(12px)`；滚动 > 40px 时启用。
- 首页的导航跟随 `container-wide`（1600），左右边缘与文章网格对齐；页脚容器同样
  放宽到 1600。内页保持 `container`（1200），导航宽度切换时带 250ms 过渡。
- 页脚保持「品牌靠左 / 统计靠右」；备案信息收进同一个 `footer-inner` 的中间列，
  用三栏栅格保证它在容器正中，避免原先单独一行时中间空出一段。
  平板（<960px）时备案另起一行居中，手机（<640px）时整体居中堆叠、统计排成 2×2。
- Logo：渐变圆形 + 紫色发光阴影。
- 操作按钮：胶囊描边样式。
- 主题/配色切换由全局 ThemeSwitcher 提供，导航内不再重复。

---

## 7. 光影与特效

### 7.1 发光阴影

```css
--glow-cyan: 0 0 20px rgba(36, 198, 220, 0.45);
--glow-purple: 0 0 20px rgba(124, 92, 255, 0.45);
--glow-pink: 0 0 20px rgba(255, 0, 153, 0.45);
```

亮色模式下降到 0.28 alpha，避免过曝。

### 7.1.1 整页背景光晕（`--page-bg-image`）

避免大面积纯黑/纯白：aurora 在 `tokens.css` 里为暗/亮两套配色各定义 3 层低透明度径向渐变，
foundation 的 `body` 以 `background-attachment: fixed` 绘制（固定视口，滚动时氛围稳定）：

```css
--page-bg-image:
  radial-gradient(1100px 620px at 10% -12%, rgba(36, 198, 220, 0.10), transparent 62%),
  radial-gradient(1000px 700px at 92% -4%, rgba(124, 92, 255, 0.14), transparent 66%),
  radial-gradient(1000px 760px at 50% 118%, rgba(255, 0, 153, 0.07), transparent 62%);
```

其它主题不定义时保持 foundation 默认 `none`（paper 即如此）。

### 7.2 径向光斑（`.aurora-blobs`，见 theme.css）

Hero/页头背景叠加 2-3 层径向渐变：
- 青色光斑 alpha 0.16（Light）/ 0.36（Dark）
- 品红光斑 alpha 0.14（Light）/ 0.30（Dark）

### 7.3 玻璃拟态

```css
.glass {
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur));
}
```

### 7.4 主题私有遮罩 Token

- `--post-hero-mask`：文章头图遮罩。
- 首页 Hero 不再使用整屏遮罩：背景图直接铺满，文字可读性由局部 `text-shadow`
  （副标题）和胶囊底色（下滑按钮）保证；导航栏在未滚动时用主题底色渐隐层托底。

组件直接 `background: var(--post-hero-mask)`，不再写 `[data-theme='light']` 分支。

### 7.5 默认封面（`.cover-fallback`）

没有上传封面图时，文章卡片 / 专栏卡片 / 文章详情头图统一使用 `theme.css` 的
`.cover-fallback`：

- 底色：`--bg-sunken → --bg-surface` 的低对比线性渐变；
- 极光：左上青、右下粉各一层弱 radial（`color-mix` 约 20%/16%），亮色下自动变淡；
- 质感：30px 细网格线（`color-mix(--text-strong 4%)`），避免整块死板；
- 不再使用整块高饱和「青→紫→粉」渐变：默认封面应该退到内容后面，只留 aurora 的氛围。

换配色时不需要改这里：所有颜色都从 Token 派生。

### 7.6 页面背景装饰（`PageDecor.vue`）

除首页 Hero 外，其余页面的背景是纯色画布。`chrome/PageDecor.vue` 在内容后面铺一层
很淡的固定装饰，避免页面显得空：

- 5 颗四角星屑（青/粉/紫交替，8s 缓慢闪烁）+ 2 朵软云（左下青、右中粉，30s 左右漂移）；
- 一层 30px 点阵（约 2% 可见度）提供纸面质感；
- 两团大范围环境光（右上紫、左下青，10%/8%），补充 body 极光光晕之外的层次；
- `aria-hidden` + `pointer-events: none`，z-index 0；内容区/页脚抬到 1；
- 窄屏只保留少量星星和一朵云，并尊重 `prefers-reduced-motion`。

这是 aurora 专属的装饰；paper 主题保持它自己的报纸版式。

### 7.7 内容背景图

管理端「网站配置 → 内容背景图」维护一个独立的图片列表（后端 `SiteConfigs` 的
`ContentBackground` Key，JSON 数组，与首页 Hero 的 `HeroBackground` 完全分开）：

- 前台 `PageDecor.vue` 每次加载随机挑一张，用 `position: fixed` 铺满视口；
- 图片先 `blur(2px)` + 轻微降饱和/压暗，再盖一层主题色遮罩（`--bg-canvas` 78%→88%），
  保证长文/列表页的正文对比度；
- 没配置时这一层不渲染，只保留 7.6 的纯色 + 星屑装饰；
- 首页 Hero 自己的背景图不受影响；本层在 Hero 下方开始可见（Hero 背景层做了底部渐隐，
  交界处也会透出其中的星屑/软云）。

### 7.8 路由切换与页面过渡

全站统一的「切换反馈」由两层组成，均来自 foundation（任何主题都能用，颜色全部走 Token）：

1. **顶部路由加载条**（`components/RouteProgress.vue` + `utils/progress.ts`）：
   路由开始 `progressStart()`，结束 `progressDone()`，导航失败 `progressFail()`（收尾变警示色）。
   进度条从 10% 起步、自动补给逼近 90%（不会自己跑满），到 100% 后停留 240ms 再淡出；
   最短显示 420ms，避免命中缓存时只闪一下。渲染是一条 3px 的极光渐变细条，
   压在导航之上（`--z-progress`）。不引 nprogress 依赖，行为与视觉都可控。
2. **页面切换过渡**（`reset.css` 的 `.page-*`）：三个 Layout 的 `RouterView` 统一用
   `name="page" mode="out-in"`。旧页面 160ms 淡出并轻微上移，新页面从下方 16px 处
   320ms 淡入，`--ease-out-expo` 收尾。只动 `opacity/transform`，不触发布局重排；
   动画结束后 `transform` 回到 `none`，不会给页面内的 `position: fixed` 元素制造包含块。

首页 Hero 的大图背景**保持完整**，不做渐隐/遮罩（按需求：大图背景不需要过渡）；
内容区顶部只保留一层静态极光柔光（`.home-section::before`）补充背景层次。
文章卡片内部的封面→正文过渡见 6.2 卡片。

---

## 8. 无障碍（WCAG AA）

- 正文对比度：Dark `#CBDAE5` on `#13141A` ≈ 12.8:1；Light `#2E333D` on `#FFFFFF` ≈ 11.2:1。
- 渐变文字仅用于装饰性大标题，不承载关键操作说明。
- 按钮最小高度 38px；焦点态使用品牌色描边/外发光。
- 尊重 `prefers-reduced-motion`（foundation/reset.css 统一降级）。

---

## 9. 页面清单（由本主题接管）

| PageId | 布局特征 |
|---|---|
| `home` | 全屏 Hero + 宽容器（1600）双栏：主区 3 列卡片（单卡约 382px，与列表页同一套卡片）+ 300px 侧栏（关于本站/社交入口/热门标签/专栏推荐/探索入口）；Hero 大图保持完整，内容区顶部保留静态极光柔光 |
| `post-list` | 居中页头 + 文章卡片网格 + 分页 |
| `post-detail` | 头图 + 标签/分类/专栏徽章 + 双栏（正文 + 目录/阅读进度）+ Giscus 评论 |
| `archive` | 居中页头 + 年份/月份时间轴 |
| `taxonomy` | 标签墙 / 分类墙（渐变胶囊，hover 发光） |
| `collection-list` | 专栏卡片网格（封面 + 简介 + 篇数） |
| `collection-detail` | 页头 + 编号文章列表 + 返回列表 |
| `not-found` | 极光背景 + 渐变 404 大字 + 返回按钮 |

---

## 10. 相关文件

- `tokens.css`：本主题两套配色的全部 Token
- `theme.css`：`.aurora-blobs` / `.gradient-line` 等极光专属装饰
- `chrome/`：Shell、NavBar、SiteFooter
- `components/`：HeroSection、PostCard、PostCardList、PaginationBar
- `pages/`：8 个主题页面
- `docs/主题系统设计.md`：Token 契约、插件接口、主题切换机制
