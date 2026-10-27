<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { deletePost, getPostDetailReadonly, getPosts, publishPost } from '@/api/posts'
import { useAuthStore } from '@/stores/auth'
import PostListRowSkeleton from '@/ui/components/skeleton/PostListRowSkeleton.vue'
import type { PagedResult, PostListItemDto } from '@/types'

const auth = useAuthStore()

const items = ref<PostListItemDto[]>([])
const total = ref(0)
const page = ref(1)
const totalPages = ref(1)
const loading = ref(false)
const errorMsg = ref('')
const okMsg = ref('')
const busyId = ref<string | null>(null)
/** 是否只看草稿 */
const onlyDrafts = ref(false)

const PAGE_SIZE = 20

const visibleItems = computed(() =>
  onlyDrafts.value ? items.value.filter((p) => !p.publishedAt) : items.value,
)
const draftCount = computed(() => items.value.filter((p) => !p.publishedAt).length)

async function load(targetPage = page.value) {
  loading.value = true
  errorMsg.value = ''
  try {
    // mine=true：后端按当前登录账号的 CreatedByUserId 过滤，只看自己创建的
    const res: PagedResult<PostListItemDto> = await getPosts({
      page: targetPage,
      pageSize: PAGE_SIZE,
      includeUnpublished: true,
      mine: true,
    })
    items.value = res.items
    total.value = res.total
    page.value = res.page
    totalPages.value = res.totalPages || 1
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))

function fmtDate(iso: string | null) {
  return iso ? iso.replace('T', ' ').slice(0, 16) : ''
}

async function withVersion<T>(p: PostListItemDto, fn: (version: number) => Promise<T>, okText: string) {
  busyId.value = p.id
  errorMsg.value = ''
  okMsg.value = ''
  try {
    // 用**只读**详情取 version，避免污染浏览量
    const detail = await getPostDetailReadonly(p.id)
    await fn(detail.version)
    okMsg.value = okText
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '操作失败'
  } finally {
    busyId.value = null
  }
}

function togglePublish(p: PostListItemDto) {
  const willPublish = !p.publishedAt
  return withVersion(p, (v) => publishPost(p.id, v, willPublish), willPublish ? '已发布' : '已下架')
}

function confirmDelete(p: PostListItemDto) {
  if (!window.confirm(`确认删除「${p.title}」？此为软删除，文章将不再出现在任何列表。`)) return
  return withVersion(p, (v) => deletePost(p.id, v), '已删除')
}

function canPrev() {
  return page.value > 1
}
function canNext() {
  return page.value < totalPages.value
}
</script>

<template>
  <section class="my-posts">
    <header class="page-head">
      <div>
        <h1 class="panel-title">我的文章</h1>
        <p class="hint">
          共 {{ total }} 篇，其中草稿 {{ draftCount }} 篇
          <template v-if="auth.user?.authorName">
            · 署名「{{ auth.user.authorName }}」
          </template>
        </p>
      </div>
      <div class="head-actions">
        <label class="filter hint">
          <input v-model="onlyDrafts" type="checkbox" />
          <span>只看草稿</span>
        </label>
        <RouterLink to="/me/posts/new" class="btn btn-primary">+ 写文章</RouterLink>
      </div>
    </header>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <PostListRowSkeleton v-if="loading" :rows="5" :columns="4" />

    <div v-else-if="visibleItems.length" class="list table">
      <div class="table-row table-head">
        <span>标题</span>
        <span>状态</span>
        <span>时间</span>
        <span class="text-right">操作</span>
      </div>
      <div v-for="p in visibleItems" :key="p.id" class="table-row">
        <span class="col-title">
          <RouterLink :to="`/post/${p.id}`" class="title-link" :title="p.title">{{ p.title }}</RouterLink>
        </span>
        <span>
          <span v-if="!p.publishedAt" class="badge badge-pill badge-warning">草稿</span>
          <span v-else class="badge badge-pill badge-success">已发布</span>
        </span>
        <span class="muted">{{ fmtDate(p.publishedAt) || '未发布' }}</span>
        <span class="text-right row-actions">
          <RouterLink class="link" :to="`/me/posts/${p.id}/edit`">编辑</RouterLink>
          <button class="link" :disabled="busyId === p.id" @click="togglePublish(p)">
            {{ p.publishedAt ? '下架' : '发布' }}
          </button>
          <button class="link link-danger" :disabled="busyId === p.id" @click="confirmDelete(p)">删除</button>
        </span>
      </div>
    </div>

    <div v-else class="empty empty-stack">
      <p>{{ onlyDrafts ? '没有草稿' : '你还没有写过文章' }}</p>
      <RouterLink to="/me/posts/new" class="btn btn-primary">写第一篇</RouterLink>
    </div>

    <nav v-if="totalPages > 1" class="pager">
      <button class="btn btn-ghost" :disabled="!canPrev() || loading" @click="load(page - 1)">上一页</button>
      <span class="pager-info">第 {{ page }} / {{ totalPages }} 页</span>
      <button class="btn btn-ghost" :disabled="!canNext() || loading" @click="load(page + 1)">下一页</button>
    </nav>
  </section>
</template>

<style scoped>
.my-posts {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--space-4);
}
.filter {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  cursor: pointer;
}

.empty-stack {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-4);
}

.list {
  overflow: hidden;
}
.table-row {
  grid-template-columns: minmax(0, 1fr) 90px 150px 190px;
}

.col-title {
  min-width: 0;
}
.title-link {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
  color: var(--text-strong);
}
.title-link:hover {
  color: var(--brand-500);
}

.pager {
  margin-top: 0;
}

@media (max-width: 880px) {
  .table-head {
    display: none;
  }
  .table-row {
    grid-template-columns: 1fr auto;
    gap: var(--space-2) var(--space-3);
  }
  .col-title {
    grid-column: 1 / -1;
    font: var(--text-body);
  }
  .table-row > span:nth-child(2) {
    justify-self: start;
  }
  .text-right {
    text-align: left;
  }
  .row-actions {
    grid-column: 1 / -1;
    justify-content: flex-start;
    padding-top: var(--space-2);
    border-top: 1px dashed var(--border-subtle);
  }
}
</style>
