<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  createCollection,
  deleteCollection,
  getCollectionById,
  getCollections,
  setCollectionPosts,
  updateCollection,
} from '@/api/collections'
import { getPosts } from '@/api/posts'
import { uploadFile } from '@/api/files'
import { ACCEPT_IMAGE } from '@/utils/media'
import FormSkeleton from '@/ui/components/skeleton/FormSkeleton.vue'
import type { CollectionDto, CollectionPayload, PostListItemDto } from '@/types'

const items = ref<CollectionDto[]>([])
const loading = ref(false)
const busy = ref(false)
const errorMsg = ref('')
const okMsg = ref('')

// ---------------------------------------------------------------- 新建 / 编辑
const showForm = ref(false)
const editingId = ref<string | null>(null)
const form = reactive<CollectionPayload>({
  title: '',
  slug: '',
  description: '',
  coverImage: '',
  sortOrder: 0,
  isPublished: true,
})
const uploadingCover = ref(false)

/**
 * 封面上传。刻意**不提供 URL 输入框**：封面地址会被直接放进 <img src>，
 * 允许手填就等于允许外部链接（访客 IP 泄露）与 javascript: / data: 之类的注入。
 * 服务端对 coverImage 也做 MediaPath 白名单校验，两层一致
 * —— 与文章封面 / 作者头像 / 站点 Logo 的处理保持一致。
 */
async function onPickCover(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) {
    errorMsg.value = '请选择图片文件'
    input.value = ''
    return
  }
  uploadingCover.value = true
  errorMsg.value = ''
  try {
    form.coverImage = await uploadFile(file)
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '封面上传失败'
  } finally {
    uploadingCover.value = false
    input.value = ''
  }
}

// ---------------------------------------------------------------- 文章编排
const managing = ref<CollectionDto | null>(null)
const manageLoading = ref(false)
const allPosts = ref<PostListItemDto[]>([])
const selectedIds = ref<string[]>([])
const manageVersion = ref(1)

const isEditing = computed(() => editingId.value !== null)

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    items.value = await getCollections(true)
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

function flash(msg: string) {
  okMsg.value = msg
  window.setTimeout(() => {
    if (okMsg.value === msg) okMsg.value = ''
  }, 2500)
}

function resetForm() {
  editingId.value = null
  form.title = ''
  form.slug = ''
  form.description = ''
  form.coverImage = ''
  form.sortOrder = items.value.length
  form.isPublished = true
}

function openCreate() {
  resetForm()
  showForm.value = true
}

function openEdit(c: CollectionDto) {
  editingId.value = c.id
  form.title = c.title
  form.slug = c.slug
  form.description = c.description
  form.coverImage = c.coverImage
  form.sortOrder = c.sortOrder
  form.isPublished = c.isPublished
  showForm.value = true
}

/** 标题自动生成 slug 草稿（仅在新建且用户未手改 slug 时） */
function onTitleInput() {
  if (isEditing.value) return
  const ascii = form.title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-')
  if (ascii) form.slug = ascii
}

async function onSubmit() {
  errorMsg.value = ''
  if (!form.title.trim()) {
    errorMsg.value = '请填写专栏标题'
    return
  }
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(form.slug.trim())) {
    errorMsg.value = '专栏标识只能包含小写字母、数字与中划线（如 my-series）'
    return
  }

  busy.value = true
  try {
    if (isEditing.value && editingId.value) {
      const target = items.value.find((c) => c.id === editingId.value)
      await updateCollection(editingId.value, { ...form, version: target?.version })
      flash('已保存')
    } else {
      await createCollection(form)
      flash('专栏已创建')
    }
    showForm.value = false
    resetForm()
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    busy.value = false
  }
}

async function onDelete(c: CollectionDto) {
  if (!window.confirm(`确认删除专栏「${c.title}」？专栏内的文章本身不会被删除。`)) return
  busy.value = true
  errorMsg.value = ''
  try {
    await deleteCollection(c.id, c.version)
    flash('专栏已删除')
    if (managing.value?.id === c.id) managing.value = null
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '删除失败'
  } finally {
    busy.value = false
  }
}

// ---------------------------------------------------------------- 文章编排
async function openManage(c: CollectionDto) {
  managing.value = c
  manageLoading.value = true
  errorMsg.value = ''
  try {
    const [detail, posts] = await Promise.all([
      getCollectionById(c.id),
      getPosts({ page: 1, pageSize: 100, includeUnpublished: true }),
    ])
    manageVersion.value = detail.version
    selectedIds.value = detail.posts.map((p) => p.id)
    allPosts.value = posts.items
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    manageLoading.value = false
  }
}

function togglePost(id: string) {
  const i = selectedIds.value.indexOf(id)
  if (i >= 0) selectedIds.value.splice(i, 1)
  else selectedIds.value.push(id)
}

function move(index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= selectedIds.value.length) return
  const arr = selectedIds.value
  ;[arr[index], arr[target]] = [arr[target], arr[index]]
}

async function saveManage() {
  if (!managing.value) return
  busy.value = true
  errorMsg.value = ''
  try {
    const detail = await setCollectionPosts(managing.value.id, selectedIds.value, manageVersion.value)
    manageVersion.value = detail.version
    flash('专栏文章已保存')
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    busy.value = false
  }
}

function selectedTitle(id: string) {
  return allPosts.value.find((p) => p.id === id)?.title ?? id
}
</script>

<template>
  <section class="admin-collections">
    <div class="toolbar">
      <span class="toolbar-info">共 {{ items.length }} 个专栏</span>
      <button class="btn btn-primary" @click="showForm ? (showForm = false) : openCreate()">
        {{ showForm ? '收起' : '+ 新建专栏' }}
      </button>
    </div>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <!-- 新建 / 编辑表单 -->
    <form v-if="showForm" class="panel panel-stack" @submit.prevent="onSubmit">
      <h3 class="panel-title">{{ isEditing ? '编辑专栏' : '新建专栏' }}</h3>

      <div class="form-grid-2">
        <label class="field">
          <span class="label">标题</span>
          <input
            v-model="form.title"
            class="input"
            maxlength="200"
            placeholder="例如：C# 进阶系列"
            @input="onTitleInput"
          />
          <span class="hint">中文标题不会自动生成 slug，请手动填写下方标识</span>
        </label>

        <label class="field">
          <span class="label">标识（slug，用于 URL）</span>
          <input v-model.trim="form.slug" class="input" maxlength="200" placeholder="csharp-advanced" />
          <span class="hint">仅小写字母、数字、中划线；访问路径 /collections/{slug}</span>
        </label>

        <label class="field">
          <span class="label">排序</span>
          <input v-model.number="form.sortOrder" class="input" type="number" min="0" />
          <span class="hint">越小越靠前</span>
        </label>
      </div>

      <div class="field">
        <span class="label">封面图</span>
        <div class="media-picker">
          <div class="media-preview" :class="{ empty: !form.coverImage }">
            <img v-if="form.coverImage" :src="form.coverImage" alt="封面预览" />
            <span v-else>未设置</span>
          </div>
          <div class="media-picker-actions">
            <div class="media-picker-buttons">
              <label class="btn btn-sm">
                {{ uploadingCover ? '上传中...' : form.coverImage ? '更换封面' : '上传封面' }}
                <input
                  type="file"
                  :accept="ACCEPT_IMAGE"
                  hidden
                  :disabled="uploadingCover"
                  @change="onPickCover"
                />
              </label>
              <button
                v-if="form.coverImage"
                type="button"
                class="btn btn-sm btn-ghost"
                :disabled="uploadingCover"
                @click="form.coverImage = ''"
              >
                移除封面
              </button>
            </div>
            <small class="hint">封面只能上传设置；不设置时用内置渐变占位</small>
          </div>
        </div>
      </div>

      <label class="field">
        <span class="label">简介</span>
        <textarea v-model="form.description" class="textarea" rows="2" maxlength="500" />
      </label>

      <label class="switch">
        <input v-model="form.isPublished" type="checkbox" />
        <span>{{ form.isPublished ? '已发布（前台可见）' : '未发布（仅管理员可见）' }}</span>
      </label>

      <footer class="panel-actions">
        <button type="button" class="btn btn-ghost" @click="((showForm = false), resetForm())">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy">
          {{ busy ? '保存中...' : isEditing ? '保存' : '创建' }}
        </button>
      </footer>
    </form>

    <FormSkeleton v-if="loading" :fields="3" />

    <div v-else class="list table">
      <div class="table-row table-head">
        <span>标题</span>
        <span>标识</span>
        <span>文章数</span>
        <span>排序</span>
        <span>状态</span>
        <span class="text-right">操作</span>
      </div>

      <div v-for="c in items" :key="c.id" class="table-row">
        <span class="col-title">{{ c.title }}</span>
        <code class="code slug">/{{ c.slug }}</code>
        <span class="muted">{{ c.postCount }} 篇</span>
        <span class="muted">{{ c.sortOrder }}</span>
        <span>
          <span v-if="c.isPublished" class="badge badge-pill badge-success">已发布</span>
          <span v-else class="badge badge-pill badge-warning">未发布</span>
        </span>
        <span class="text-right row-actions">
          <button class="link" :disabled="busy" @click="openManage(c)">编排文章</button>
          <button class="link" :disabled="busy" @click="openEdit(c)">编辑</button>
          <button class="link link-danger" :disabled="busy" @click="onDelete(c)">删除</button>
        </span>
      </div>

      <p v-if="!items.length" class="empty">还没有专栏</p>
    </div>

    <!-- 文章编排 -->
    <div v-if="managing" class="panel panel-stack">
      <h3 class="panel-title">编排文章 · {{ managing.title }}</h3>
      <p class="hint">
        勾选属于该专栏的文章，用箭头调整**专栏内阅读顺序**。一篇文章可同时属于多个专栏。
      </p>

      <div v-if="manageLoading" class="empty">加载中...</div>

      <div v-else class="manage-grid">
        <div class="pane">
          <h4>可选文章（{{ allPosts.length }}）</h4>
          <label v-for="p in allPosts" :key="p.id" class="opt">
            <input
              type="checkbox"
              :checked="selectedIds.includes(p.id)"
              @change="togglePost(p.id)"
            />
            <span class="opt-title">{{ p.title }}</span>
            <span v-if="!p.publishedAt" class="badge badge-pill badge-warning">草稿</span>
          </label>
          <p v-if="!allPosts.length" class="muted">还没有文章</p>
        </div>

        <div class="pane">
          <h4>专栏内顺序（{{ selectedIds.length }}）</h4>
          <ol>
            <li v-for="(id, i) in selectedIds" :key="id" class="ordered-item">
              <span class="badge badge-pill badge-brand idx">{{ i + 1 }}</span>
              <span class="opt-title">{{ selectedTitle(id) }}</span>
              <span class="order-actions">
                <button class="btn btn-sm btn-icon btn-ghost" :disabled="i === 0" @click="move(i, -1)">↑</button>
                <button class="btn btn-sm btn-icon btn-ghost" :disabled="i === selectedIds.length - 1" @click="move(i, 1)">↓</button>
                <button class="btn btn-sm btn-icon btn-ghost" @click="togglePost(id)">×</button>
              </span>
            </li>
          </ol>
          <p v-if="!selectedIds.length" class="muted">左侧勾选文章加入本专栏</p>
        </div>
      </div>

      <footer class="panel-actions">
        <button class="btn btn-ghost" @click="managing = null">关闭</button>
        <button class="btn btn-primary" :disabled="busy || manageLoading" @click="saveManage">
          {{ busy ? '保存中...' : '保存编排' }}
        </button>
      </footer>
    </div>
  </section>
</template>

<style scoped>
.admin-collections {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
.toolbar {
  margin-bottom: 0;
}

.panel-actions {
  margin-top: 0;
}

.list {
  overflow: hidden;
}
.table-row {
  grid-template-columns: minmax(0, 1.3fr) 140px 80px 60px 90px 220px;
}

.col-title {
  color: var(--text-strong);
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.slug {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 文章编排：左侧候选、右侧已选顺序 */
.manage-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-6);
}

.pane {
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  padding: var(--space-4);
  max-height: 420px;
  overflow-y: auto;
}
.pane h4 {
  font: var(--text-body-sm);
  color: var(--text-muted);
  margin-bottom: var(--space-2);
}

.opt {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) 0;
  cursor: pointer;
  border-bottom: 1px dashed var(--border-subtle);
}
.opt:last-child {
  border-bottom: none;
}
.opt-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font: var(--text-body-sm);
  color: var(--text-default);
}

.ordered-item {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) 0;
  border-bottom: 1px dashed var(--border-subtle);
}
.ordered-item:last-child {
  border-bottom: none;
}
.idx {
  width: 22px;
  height: 22px;
  padding: 0;
  display: grid;
  place-items: center;
  flex-shrink: 0;
}
.order-actions {
  display: flex;
  gap: var(--space-1);
}

@media (max-width: 1100px) {
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
  .text-right {
    text-align: left;
  }
  .row-actions {
    grid-column: 1 / -1;
    justify-content: flex-start;
    padding-top: var(--space-2);
    border-top: 1px dashed var(--border-subtle);
  }
  .manage-grid {
    grid-template-columns: 1fr;
  }
}
</style>
