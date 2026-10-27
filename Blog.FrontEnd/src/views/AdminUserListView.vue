<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  createUser,
  disableUser,
  getUsers,
  resetUserPassword,
  updateUser,
  type UserListItem,
} from '@/api/users'
import { getAuthors } from '@/api/authors'
import { useAuthStore } from '@/stores/auth'
import type { AuthorDto, UserRole } from '@/types'

const auth = useAuthStore()

const users = ref<UserListItem[]>([])
const authors = ref<AuthorDto[]>([])
const loading = ref(false)
const busy = ref(false)
const errorMsg = ref('')
const okMsg = ref('')

// ---------------------------------------------------------------- 新建账号
const showCreate = ref(false)
const createForm = reactive({
  email: '',
  password: '',
  role: 'Author' as UserRole,
  authorId: '' as string,
})

// ---------------------------------------------------------------- 行内编辑
const editingId = ref<string | null>(null)
const editForm = reactive({
  role: 'Author' as UserRole,
  authorId: '' as string,
  isActive: true,
})

// ---------------------------------------------------------------- 重置密码
const resetId = ref<string | null>(null)
const newPassword = ref('')

const authorName = computed(() => (id: string | null) =>
  id ? authors.value.find((a) => a.id === id)?.name ?? '(已删除的作者)' : '',
)

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const [u, a] = await Promise.all([getUsers(), getAuthors()])
    users.value = u
    authors.value = a
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

async function onCreate() {
  errorMsg.value = ''
  if (!createForm.email.trim() || !createForm.password) {
    errorMsg.value = '邮箱与密码不能为空'
    return
  }
  if (createForm.password.length < 8) {
    errorMsg.value = '密码长度不能少于 8 位'
    return
  }

  busy.value = true
  try {
    await createUser({
      email: createForm.email.trim(),
      password: createForm.password,
      role: createForm.role,
      authorId: createForm.authorId || null,
    })
    showCreate.value = false
    createForm.email = ''
    createForm.password = ''
    createForm.role = 'Author'
    createForm.authorId = ''
    flash('账号已创建')
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '创建失败'
  } finally {
    busy.value = false
  }
}

function startEdit(u: UserListItem) {
  editingId.value = u.id
  editForm.role = u.role
  editForm.authorId = u.authorId ?? ''
  editForm.isActive = u.isActive
  errorMsg.value = ''
}

function cancelEdit() {
  editingId.value = null
}

async function saveEdit(u: UserListItem) {
  busy.value = true
  errorMsg.value = ''
  try {
    await updateUser(u.id, {
      role: editForm.role,
      authorId: editForm.authorId || null,
      isActive: editForm.isActive,
      version: u.version,
    })
    editingId.value = null
    flash('已保存')
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    busy.value = false
  }
}

async function onDisable(u: UserListItem) {
  if (!window.confirm(`确认停用账号「${u.email}」？停用后无法登录，且其已签发的 token 立即失效。`)) return
  busy.value = true
  errorMsg.value = ''
  try {
    await disableUser(u.id, u.version)
    flash('已停用')
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '停用失败'
  } finally {
    busy.value = false
  }
}

async function onResetPassword(u: UserListItem) {
  errorMsg.value = ''
  if (newPassword.value.length < 8) {
    errorMsg.value = '密码长度不能少于 8 位'
    return
  }
  busy.value = true
  try {
    await resetUserPassword(u.id, newPassword.value, u.version)
    resetId.value = null
    newPassword.value = ''
    flash('密码已重置，该账号需用新密码重新登录')
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '重置失败'
  } finally {
    busy.value = false
  }
}

function fmt(iso: string | null) {
  return iso ? iso.replace('T', ' ').slice(0, 16) : '—'
}

/** 不允许停用自己，避免把自己锁在门外 */
function isSelf(u: UserListItem) {
  return u.id === auth.user?.id
}
</script>

<template>
  <section class="admin-users">
    <div class="toolbar">
      <span class="toolbar-info">共 {{ users.length }} 个账号</span>
      <button class="btn btn-primary" @click="showCreate = !showCreate">
        {{ showCreate ? '收起' : '+ 新建账号' }}
      </button>
    </div>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <!-- 新建账号 -->
    <form v-if="showCreate" class="panel panel-stack" @submit.prevent="onCreate">
      <h3 class="panel-title">新建账号</h3>
      <p class="panel-hint">
        作者账号由此创建（系统不开放自助注册）。创建后把邮箱与初始密码告知作者。
      </p>

      <div class="form-grid-2">
        <label class="field">
          <span class="label">邮箱</span>
          <input v-model="createForm.email" class="input" type="email" placeholder="author@example.com" />
        </label>

        <label class="field">
          <span class="label">初始密码</span>
          <input
            v-model="createForm.password"
            class="input"
            type="password"
            placeholder="至少 8 位"
            autocomplete="new-password"
          />
        </label>

        <label class="field">
          <span class="label">角色</span>
          <select v-model="createForm.role" class="select">
            <option value="Author">Author（内容作者）</option>
            <option value="Admin">Admin（站点管理员）</option>
          </select>
        </label>

        <label class="field">
          <span class="label">关联作者（内容署名）</span>
          <select v-model="createForm.authorId" class="select">
            <option value="">不关联</option>
            <option v-for="a in authors" :key="a.id" :value="a.id">{{ a.name }}</option>
          </select>
          <span class="hint">
            角色为 Author 时建议关联：决定他发布文章的署名对象（不是登录身份）
          </span>
        </label>
      </div>

      <footer class="panel-actions">
        <button type="button" class="btn btn-ghost" @click="showCreate = false">取消</button>
        <button type="submit" class="btn btn-primary" :disabled="busy">创建</button>
      </footer>
    </form>

    <div v-if="loading" class="empty">加载中...</div>

    <div v-else class="list table">
      <div class="table-row table-head">
        <span>邮箱</span>
        <span>角色</span>
        <span>关联作者</span>
        <span>状态</span>
        <span>最近登录</span>
        <span class="text-right">操作</span>
      </div>

      <div v-for="u in users" :key="u.id" class="table-row">
        <template v-if="editingId === u.id">
          <span class="col-email">{{ u.email }}</span>
          <select v-model="editForm.role" class="select input-sm">
            <option value="Author">Author</option>
            <option value="Admin">Admin</option>
          </select>
          <select v-model="editForm.authorId" class="select input-sm">
            <option value="">不关联</option>
            <option v-for="a in authors" :key="a.id" :value="a.id">{{ a.name }}</option>
          </select>
          <label class="switch">
            <input v-model="editForm.isActive" type="checkbox" />
            <span>{{ editForm.isActive ? '启用' : '停用' }}</span>
          </label>
          <span class="muted">v{{ u.version }}</span>
          <span class="text-right row-actions">
            <button class="link" :disabled="busy" @click="saveEdit(u)">保存</button>
            <button class="link link-muted" :disabled="busy" @click="cancelEdit">取消</button>
          </span>
        </template>

        <template v-else>
          <span class="col-email">
            {{ u.email }}
            <span v-if="isSelf(u)" class="badge badge-pill badge-brand">我自己</span>
          </span>
          <span>
            <span class="badge badge-pill" :class="u.role === 'Admin' ? 'badge-accent' : 'badge-brand'">{{ u.role }}</span>
          </span>
          <span class="muted">{{ authorName(u.authorId) || '—' }}</span>
          <span>
            <span v-if="u.isActive" class="badge badge-pill badge-success">启用</span>
            <span v-else class="badge badge-pill badge-danger">已停用</span>
          </span>
          <span class="muted">{{ fmt(u.lastLoginAt) }}</span>
          <span class="text-right row-actions">
            <button class="link" :disabled="busy" @click="startEdit(u)">编辑</button>
            <button class="link" :disabled="busy" @click="resetId = resetId === u.id ? null : u.id">
              重置密码
            </button>
            <button
              v-if="!isSelf(u) && u.isActive"
              class="link link-danger"
              :disabled="busy"
              @click="onDisable(u)"
            >
              停用
            </button>
          </span>
        </template>

        <!-- 重置密码行 -->
        <span v-if="resetId === u.id" class="reset-row">
          <input
            v-model="newPassword"
            class="input input-sm"
            type="password"
            placeholder="新密码（至少 8 位）"
            autocomplete="new-password"
          />
          <button class="link" :disabled="busy" @click="onResetPassword(u)">确认重置</button>
          <button class="link link-muted" @click="((resetId = null), (newPassword = ''))">取消</button>
          <span class="hint">重置后该账号所有已签发 token 立即失效</span>
        </span>
      </div>
    </div>

    <p class="hint">
      说明：本页只管理**登录账号**。文章的署名对象（Author）是内容，属「内容层」，
      需要单独的「作者管理」页面。
    </p>
  </section>
</template>

<style scoped>
.admin-users {
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
  grid-template-columns: minmax(0, 1.4fr) 90px 110px 80px 130px 230px;
}

.col-email {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-strong);
  font-weight: 600;
}

.reset-row {
  grid-column: 1 / -1;
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
  padding-top: var(--space-2);
  border-top: 1px dashed var(--border-subtle);
}

@media (max-width: 1100px) {
  .table-head {
    display: none;
  }
  .table-row {
    grid-template-columns: 1fr auto;
    gap: var(--space-2) var(--space-3);
  }
  .col-email {
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
