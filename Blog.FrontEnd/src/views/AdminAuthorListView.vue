<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import {
  createAuthor,
  deleteAuthor,
  getAuthors,
  updateAuthor,
  type CreateAuthorPayload,
} from '@/api/authors'
import { uploadFile } from '@/api/files'
import { ACCEPT_IMAGE } from '@/utils/media'
import { useSiteStore } from '@/stores/site'
import type { AuthorDto } from '@/types'

const site = useSiteStore()

const items = ref<AuthorDto[]>([])
const loading = ref(false)
const busy = ref(false)
const errorMsg = ref('')
const okMsg = ref('')

const showForm = ref(false)
const editingId = ref<string | null>(null)
const uploading = ref(false)

const form = reactive<CreateAuthorPayload>({
  name: '',
  email: '',
  bio: '',
  avatar: '',
})

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    items.value = await getAuthors()
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
  form.name = ''
  form.email = ''
  form.bio = ''
  form.avatar = ''
}

function openCreate() {
  resetForm()
  showForm.value = true
}

function openEdit(a: AuthorDto) {
  editingId.value = a.id
  form.name = a.name
  form.email = a.email
  form.bio = a.bio
  form.avatar = a.avatar
  showForm.value = true
}

async function onPickAvatar(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) {
    errorMsg.value = '请选择图片文件'
    input.value = ''
    return
  }
  uploading.value = true
  errorMsg.value = ''
  try {
    form.avatar = await uploadFile(file)
    flash('头像上传成功，记得保存')
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '上传失败'
  } finally {
    uploading.value = false
    input.value = ''
  }
}

async function onSubmit() {
  errorMsg.value = ''
  if (!form.name.trim() || !form.email.trim()) {
    errorMsg.value = '姓名与邮箱不能为空'
    return
  }

  busy.value = true
  try {
    if (editingId.value) {
      const target = items.value.find((a) => a.id === editingId.value)
      await updateAuthor(editingId.value, {
        name: form.name.trim(),
        email: form.email.trim(),
        bio: form.bio,
        avatar: form.avatar,
        // 不做兜底：列表里找不到就用 undefined，后端会返回 4001「缺少合法的版本号」。
        // 以前写的是 `?? 1` —— 那是**猜**一个版本号发出去，猜对了反而绕过乐观锁。
        version: target?.version,
      })
      flash('已保存')
    } else {
      await createAuthor({
        name: form.name.trim(),
        email: form.email.trim(),
        bio: form.bio,
        avatar: form.avatar,
      })
      flash('作者已创建')
    }
    showForm.value = false
    resetForm()
    await load()
    // NavBar / Footer 的站点信息与文章详情的作者信息都来自缓存
    await site.refreshAll()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    busy.value = false
  }
}

async function onDelete(a: AuthorDto) {
  if (
    !window.confirm(
      `确认删除作者「${a.name}」？\n这是软删除：其署名文章的作者会被置空，文章本身不会被删除。`,
    )
  )
    return

  busy.value = true
  errorMsg.value = ''
  try {
    await deleteAuthor(a.id, a.version)
    flash('作者已删除')
    await load()
    await site.refreshAll()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '删除失败'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="admin-authors">
    <div class="toolbar">
      <span class="toolbar-info">共 {{ items.length }} 位作者</span>
      <button class="btn btn-primary" @click="showForm ? ((showForm = false), resetForm()) : openCreate()">
        {{ showForm ? '收起' : '+ 新建作者' }}
      </button>
    </div>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <!-- 新建 / 编辑 -->
    <form v-if="showForm" class="panel panel-stack" @submit.prevent="onSubmit">
      <h3 class="panel-title">{{ editingId ? '编辑作者' : '新建作者' }}</h3>
      <p class="panel-hint">
        作者是**文章的署名对象**（内容层），不是登录账号。给作者开通登录请到「账号管理」。
      </p>

      <div class="media-picker">
        <div class="media-preview round">
          <img v-if="form.avatar" :src="form.avatar" alt="头像" />
          <span v-else class="media-fallback initial">{{ form.name.slice(0, 1) || '?' }}</span>
        </div>
        <div class="media-picker-actions">
          <div class="media-picker-buttons">
            <label class="btn btn-ghost">
              {{ uploading ? '上传中...' : form.avatar ? '更换头像' : '上传头像' }}
              <input type="file" :accept="ACCEPT_IMAGE" hidden :disabled="uploading" @change="onPickAvatar" />
            </label>
            <button
              v-if="form.avatar"
              type="button"
              class="btn btn-ghost"
              :disabled="uploading"
              @click="form.avatar = ''"
            >
              移除头像
            </button>
          </div>
          <!-- 刻意不提供 URL 输入框：头像地址会被直接放进 <img src>，
               允许手填就等于允许外部链接（访客 IP 泄露）与 javascript:/data: 之类的注入。
               服务端 MediaPath 也是白名单校验，两层一致。 -->
          <span class="hint">头像只能上传设置，支持 jpg / png / webp / gif，单文件不超过 10MB</span>
        </div>
      </div>

      <div class="form-grid-2">
        <label class="field">
          <span class="label">姓名</span>
          <input v-model="form.name" class="input" maxlength="100" placeholder="展示在文章页的作者名" />
        </label>
        <label class="field">
          <span class="label">邮箱（仅展示/联系用）</span>
          <input v-model="form.email" class="input" maxlength="100" placeholder="author@example.com" />
        </label>
      </div>

      <label class="field">
        <span class="label">个人简介</span>
        <textarea v-model="form.bio" class="textarea" rows="3" maxlength="500" />
      </label>

      <footer class="panel-actions">
        <button type="button" class="btn btn-ghost" @click="((showForm = false), resetForm())">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy || uploading">
          {{ busy ? '保存中...' : editingId ? '保存' : '创建' }}
        </button>
      </footer>
    </form>

    <div v-if="loading" class="empty">加载中...</div>

    <div v-else class="list table">
      <div class="table-row table-head">
        <span>作者</span>
        <span>邮箱</span>
        <span>简介</span>
        <span>版本</span>
        <span class="text-right">操作</span>
      </div>

      <div v-for="a in items" :key="a.id" class="table-row">
        <span class="col-author">
          <span class="avatar avatar-fallback">
            <img v-if="a.avatar" class="avatar-img" :src="a.avatar" :alt="a.name" />
            <template v-else>{{ a.name.slice(0, 1) }}</template>
          </span>
          <span class="name">{{ a.name }}</span>
        </span>
        <span class="muted ellipsis">{{ a.email }}</span>
        <span class="muted ellipsis">{{ a.bio || '—' }}</span>
        <span class="muted">v{{ a.version }}</span>
        <span class="text-right row-actions">
          <button class="link" :disabled="busy" @click="openEdit(a)">编辑</button>
          <button class="link link-danger" :disabled="busy" @click="onDelete(a)">删除</button>
        </span>
      </div>

      <p v-if="!items.length" class="empty">还没有作者</p>
    </div>

    <p class="hint">
      说明：本页维护的是**署名信息**。登录账号（邮箱/密码/角色）在「账号管理」里维护，
      并通过「关联作者」把某个账号绑定到这里的作者。
    </p>
  </section>
</template>

<style scoped>
.admin-authors {
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
  grid-template-columns: minmax(0, 1.1fr) minmax(0, 1.2fr) minmax(0, 1.6fr) 60px 140px;
}

.col-author {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
}
.avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.name {
  color: var(--text-strong);
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 1100px) {
  .table-head {
    display: none;
  }
  .table-row {
    grid-template-columns: 1fr auto;
    gap: var(--space-2) var(--space-3);
  }
  .col-author {
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
}
</style>
