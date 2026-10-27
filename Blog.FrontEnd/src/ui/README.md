# src/ui —— 独立 UI 层

> 完整契约见仓库根目录 [`docs/主题系统设计.md`](../../../docs/主题系统设计.md)。
> 这份 README 只做代码导航与常用操作说明。

## 目录

```
ui/
├── index.ts              入口：注入 foundation 样式、注册内置主题、导出 API
├── contracts/            PageId + 页面模型（Page Model）+ 公开站导航项
├── runtime/              主题插件类型、注册表（extends 继承）、Pinia store、DOM 应用
├── foundation/           reset / Token 契约 / 通用组件类（唯一基础样式层）
├── components/           主题无关组件：ThemeSwitcher、SiteLogo、SearchModal、SocialIcon、骨架屏、Giscus
└── themes/
    ├── aurora/           默认主题：极光霓虹（dark/light），顶部导航 + Hero + 双栏详情
    └── paper/            第二主题：墨纸（ink/dusk），左侧栏 + 单栏正文 + 索引式归档
```

## 三条规则

1. **视觉只准出现在 `src/ui/`**：页面上不写颜色/字体/圆角/阴影字面量，用 Token 与
   `foundation/primitives.css` 的通用类。后台页面的 scoped 样式只留布局（grid/flex/尺寸）。
2. **公开站页面 = 数据控制器**：`src/views/*.vue` 负责取数与错误处理，模板只有
   `<component :is="page" v-if="page" :model="model" />`；`useThemePage('home')` 取当前主题页面。
3. **主题包内部不要 import `@/ui` barrel**（会与 `ui/index.ts` 注册主题形成静态环），
   用具名模块：`@/ui/runtime/store`、`@/ui/contracts/pages`、`@/ui/components/SiteLogo.vue`。

## 新增一个主题

1. 新建 `src/ui/themes/<id>/`：
   - `tokens.css`：在 `[data-ui-theme='<id>'][data-color-mode='<mode>']` 下覆盖
     `foundation/tokens.css` 的全部契约 Token（可以先用 aurora 的 tokens.css 对照）；
   - `theme.css`（可选）：主题专属全局装饰，选择器都带上 `[data-ui-theme='<id>']`；
   - `chrome/Shell.vue`：页面外壳，默认插槽 = 路由页面（导航/页脚/搜索都放这里）；
   - `pages/*.vue`：8 个公开站页面，`defineProps<{ model: XxxPageModel }>()`；
   - `index.ts`：导出 `ThemePlugin`（id/name/modes/defaultMode/preview/shell/pages），
     并 `import './tokens.css'`。
2. 在 `src/ui/themes/index.ts` 的 `registerBuiltinThemes()` 里 `registerTheme(theme)`。
   （外部插件则运行时调用 `registerTheme(plugin)`，可通过 `plugin.css` 注入样式。）
3. `npm run build` 后切换器会自动出现新主题；未实现的页面可用 `extends: '<父主题 id>'`
   回退到父主题。

## 主题 / 配色

- 状态：`useThemeStore()`（`src/ui/runtime/store.ts`），持久化 `blog-ui-theme` / `blog-ui-mode`；
- DOM：`<html data-ui-theme="aurora" data-color-mode="dark">`；首屏由 `index.html` 内联脚本预写；
- 站点默认值：`Blog.FrontEnd/.env` 的 `VITE_DEFAULT_THEME` / `VITE_DEFAULT_COLOR_MODE`；
- 配色模式数量由主题决定（aurora: dark/light；paper: ink/dusk），切换器自动渲染。

## 验证

```bash
cd Blog.FrontEnd
npm run build     # vue-tsc -b && vite build
npm test          # markdownImport + 主题注册表/继承链单测
```
