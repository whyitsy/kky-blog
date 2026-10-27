import type { ThemePlugin } from '../../runtime/types'
import './tokens.css'
import './theme.css'
import Shell from './chrome/Shell.vue'
import HomePage from './pages/HomePage.vue'
import PostListPage from './pages/PostListPage.vue'
import PostDetailPage from './pages/PostDetailPage.vue'
import ArchivePage from './pages/ArchivePage.vue'
import TaxonomyPage from './pages/TaxonomyPage.vue'
import CollectionListPage from './pages/CollectionListPage.vue'
import CollectionDetailPage from './pages/CollectionDetailPage.vue'
import NotFoundPage from './pages/NotFoundPage.vue'

/**
 * Paper · 墨纸（第二主题）
 *
 * 视觉取向：编辑部 / 文学刊物。衬线标题与正文、暖纸色底、方正少圆角，
 * 用 1px 细线和留白建立层次；没有霓虹、没有渐变光斑、没有玻璃拟态。
 *
 * 布局与 aurora 的差异（这是本主题存在的意义——证明换主题 = 换布局）：
 *   · 外壳：左侧固定侧栏（目录式导航 + 社交 + 统计），而非顶部毛玻璃导航；
 *   · 首页：报刊 masthead + 编号文章列表，而非全屏 Hero；
 *   · 详情：单栏 720px 正文 + 折叠目录，而非「正文 + 右侧目录栏」双栏；
 *   · 归档：年份左栏的索引表，而非时间轴；
 *   · 列表/专栏：编辑部式索引与编号清单，而非卡片网格。
 */
const paper: ThemePlugin = {
  id: 'paper',
  name: '墨纸 Paper',
  description: '编辑部气质：左侧目录 + 单栏正文 + 暖纸配色，用细线与留白排版',
  modes: [
    { id: 'ink', label: '纸白', scheme: 'light' },
    { id: 'dusk', label: '墨夜', scheme: 'dark' },
  ],
  defaultMode: 'ink',
  preview: {
    background: '#f7f3ea',
    surface: '#fcf9f2',
    text: '#1c1a16',
    primary: '#8a2f28',
    accent: '#5c5d2c',
  },
  shell: Shell,
  pages: {
    home: HomePage,
    'post-list': PostListPage,
    'post-detail': PostDetailPage,
    archive: ArchivePage,
    taxonomy: TaxonomyPage,
    'collection-list': CollectionListPage,
    'collection-detail': CollectionDetailPage,
    'not-found': NotFoundPage,
  },
}

export default paper
