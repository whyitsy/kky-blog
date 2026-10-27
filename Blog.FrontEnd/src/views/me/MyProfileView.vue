<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { getMyAuthor, updateAuthor } from '@/api/authors'
import { uploadFile } from '@/api/files'
import { ACCEPT_IMAGE } from '@/utils/media'
import { useAuthStore } from '@/stores/auth'
import { useSiteStore } from '@/stores/site'
import type { AuthorDto } from '@/types'

const auth = useAuthStore()
const site = useSiteStore()

const author = ref<AuthorDto | null>(null)
const loading = ref(true)
const saving = ref(false)
const uploading = ref(false)
const errorMsg = ref('')
const okMsg = ref('')
/** 账号未关联作者时的专门提示（不是错误，是待管理员处理的状态） */
const notLinked = ref(false)

const form = reactive({ name: '', email: '', bio: '', avatar: '' })
const avatarBroken = ref(false)

function fill(a: AuthorDto) {
  form.name = a.name
  form.email = a.email
  form.bio = a.bio
  form.avatar = a.avatar
  avatarBroken.value = false
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  notLinked.value = false
  try {
    const a = await getMyAuthor()
    author.value = a
    fill(a)
  } catch (e) {
    // 后端在「账号未关联作者」时返回 404 并给出可操作提示
    notLinked.value = true
    errorMsg.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

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
    avatarBroken.value = false
    okMsg.value = '头像上传成功，记得保存'
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '上传失败'
  } finally {
    uploading.value = false
    input.value = ''
  }
}

async function onSave() {
  if (!author.value) return
  if (!form.name.trim() || !form.email.trim()) {
    errorMsg.value = '姓名与邮箱不能为空'
    return
  }

  saving.value = true
  errorMsg.value = ''
  okMsg.value = ''
  try {
    const updated = await updateAuthor(author.value.id, {
      name: form.name.trim(),
      email: form.email.trim(),
      bio: form.bio,
      avatar: form.avatar,
      version: author.value.version,
    })
    author.value = updated
    fill(updated)
    okMsg.value = '已保存'
    // 文章详情与首屏站点信息都缓存了作者信息，保存后刷新
    await site.refreshAll()
    // 顺带刷新登录用户里的 authorName（顶栏显示用）
    await auth.restore()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    saving.value = false
  }
}

function onReset() {
  if (author.value) fill(author.value)
  errorMsg.value = ''
  okMsg.value = ''
}
</script>

<template>
  <section class="my-profile">
    <header class="page-head">
      <div>
        <h1 class="panel-title">个人资料</h1>
        <p class="hint">这里的姓名与头像会显示在你发布的文章上</p>
      </div>
    </header>

    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <div v-if="loading" class="empty">加载中...</div>

    <!-- 账号未关联作者：给出明确指引，而不是让用户对着空表单猜 -->
    <div v-else-if="notLinked" class="panel panel-stack unlinked">
      <h3 class="panel-title">账号未关联作者</h3>
      <p>
        你的登录账号（{{ auth.user?.email }}）还没有关联到任何作者。
        作者是文章的署名对象，需要由管理员在「账号管理 → 编辑 → 关联作者」中为你指定。
      </p>
      <p class="muted">在关联之前，你可以正常写文章，但文章不会显示你的署名信息。</p>
    </div>

    <form v-else-if="author" class="panel panel-stack" @submit.prevent="onSave">
      <div class="media-picker">
        <div class="media-preview round avatar-box">
          <img v-if="form.avatar && !avatarBroken" :src="form.avatar" alt="头像" @error="avatarBroken = true" />
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
          <!-- 同管理端：不提供 URL 输入框，头像只能上传（服务端 MediaPath 白名单是第二道闸） -->
          <span class="hint">头像只能上传设置，支持 jpg / png / webp / gif，单文件不超过 10MB</span>
        </div>
      </div>

      <label class="field">
        <span class="label">姓名</span>
        <input v-model="form.name" class="input" maxlength="100" placeholder="展示在文章页的作者名" />
      </label>

      <label class="field">
        <span class="label">邮箱（仅展示/联系用）</span>
        <input v-model="form.email" class="input" maxlength="100" placeholder="author@example.com" />
      </label>

      <label class="field">
        <span class="label">个人简介</span>
        <textarea v-model="form.bio" class="textarea" rows="4" maxlength="500" />
      </label>

      <footer class="form-actions">
        <span class="hint">版本号 v{{ author.version }}</span>
        <div class="actions">
          <button type="button" class="btn btn-ghost" :disabled="saving" @click="onReset">重置</button>
          <button type="submit" class="btn btn-primary" :disabled="saving || uploading">
            {{ saving ? '保存中...' : '保存' }}
          </button>
        </div>
      </footer>
    </form>

    <div v-else class="empty">暂无作者资料</div>
  </section>
</template>

<style scoped>
.my-profile {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.page-head h1 {
  margin-bottom: var(--space-1);
}

.avatar-box {
  width: 88px;
  height: 88px;
}

.unlinked p {
  font: var(--text-body-sm);
  color: var(--text-default);
  line-height: 1.9;
}

.form-actions {
  justify-content: space-between;
}
.actions {
  display: flex;
  gap: var(--space-3);
}
</style>
