<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import PostEditor from '@/components/admin/PostEditor.vue'
import { createPost } from '@/api/posts'
import type { PostPayload } from '@/types'

const router = useRouter()
const saving = ref(false)
const errorMsg = ref('')

async function onSave(payload: PostPayload) {
  saving.value = true
  errorMsg.value = ''
  try {
    await createPost(payload)
    // 回到我的文章列表（作者工作区），而不是详情页：
    // 作者更关心「我的列表里有没有它」，且草稿跳到详情页只会 404
    router.push({ name: 'my-posts' })
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '创建失败'
  } finally {
    saving.value = false
  }
}

function onCancel() {
  router.push({ name: 'my-posts' })
}
</script>

<template>
  <section class="my-post-new">
    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <PostEditor mode="create" :saving="saving" @save="onSave" @cancel="onCancel" />
  </section>
</template>

<style scoped>
.my-post-new {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  max-width: 880px;
}
</style>
