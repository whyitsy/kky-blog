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
 * Aurora · 极光霓虹（默认主题）
 *
 * 视觉来源：参考设计规范 v2（暗色优先、玻璃拟态、极光渐变、双栏文章详情）。
 * 布局特征：顶部固定毛玻璃导航 + 全屏 Hero + 居中容器 + 文章详情「正文 + 右侧目录」。
 */
const aurora: ThemePlugin = {
  id: 'aurora',
  name: '极光 Aurora',
  description: '暗色优先的霓虹渐变风，玻璃导航 + 极光光斑',
  modes: [
    { id: 'dark', label: '暗色', scheme: 'dark' },
    { id: 'light', label: '亮色', scheme: 'light' },
  ],
  defaultMode: 'dark',
  preview: {
    background: '#13141a',
    surface: '#1b1d24',
    text: '#f2f5f9',
    primary: '#22c5dc',
    accent: '#ff0099',
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

export default aurora
