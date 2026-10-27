<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { CategoryDto, TagDto } from '@/types'

/** 分类与标签的字段/接口完全一致，只有文案与端点不同，因此共用一套管理 UI */
type TaxonomyKind = 'category' | 'tag'
type TaxonomyItem = CategoryDto | TagDto

const props = defineProps<{
  kind: TaxonomyKind
  /** 是否展示「文章数」列 */
  showPostCount?: boolean
  load: () => Promise<TaxonomyItem[]>
  create: (name: string) => Promise<TaxonomyItem>
  update: (id: string, name: string, version: number) => Promise<TaxonomyItem>
  remove: (id: string, version: number) => Promise<null>
}>()

const items = ref<TaxonomyItem[]>([])
const loading = ref(false)
const busy = ref(false)
const errorMsg = ref('')
const okMsg = ref('')

const newName = ref('')
const editingId = ref<string | null>(null)
const editingName = ref('')

function label(_singular = false) {
  return props.kind === 'category' ? '分类' : '标签'
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    items.value = await props.load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function onCreate() {
  const name = newName.value.trim()
  if (!name) {
    errorMsg.value = `请填写${label(true)}名称`
    return
  }

  busy.value = true
  errorMsg.value = ''
  okMsg.value = ''
  try {
    await props.create(name)
    newName.value = ''
    okMsg.value = '已新增'
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '新增失败'
  } finally {
    busy.value = false
  }
}

function startEdit(item: TaxonomyItem) {
  editingId.value = item.id
  editingName.value = item.name
  errorMsg.value = ''
  okMsg.value = ''
}

function cancelEdit() {
  editingId.value = null
  editingName.value = ''
}

async function saveEdit(item: TaxonomyItem) {
  const name = editingName.value.trim()
  if (!name) {
    errorMsg.value = `请填写${label(true)}名称`
    return
  }
  if (name === item.name) {
    cancelEdit()
    return
  }

  busy.value = true
  errorMsg.value = ''
  okMsg.value = ''
  try {
    await props.update(item.id, name, item.version)
    okMsg.value = '已保存'
    cancelEdit()
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '保存失败'
  } finally {
    busy.value = false
  }
}

async function onDelete(item: TaxonomyItem) {
  const extra =
    props.kind === 'category'
      ? '该分类下文章的所属分类将被置空。'
      : '文章与该标签的关联会一并失效。'
  if (!window.confirm(`确认删除「${item.name}」？${extra}`)) return

  busy.value = true
  errorMsg.value = ''
  okMsg.value = ''
  try {
    await props.remove(item.id, item.version)
    okMsg.value = '已删除'
    await load()
  } catch (e) {
    errorMsg.value = e instanceof Error ? e.message : '删除失败'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="taxonomy">
    <p v-if="errorMsg" class="banner banner-error">{{ errorMsg }}</p>
    <p v-if="okMsg" class="banner banner-success">{{ okMsg }}</p>

    <form class="add-bar panel" @submit.prevent="onCreate">
      <input
        v-model="newName"
        class="input"
        maxlength="100"
        :placeholder="`新增${label(true)}，例如「${kind === 'category' ? '后端' : 'Vue'}」`"
      />
      <button type="submit" class="btn btn-primary" :disabled="busy || !newName.trim()">新增</button>
    </form>

    <div v-if="loading" class="empty">加载中...</div>

    <div v-else-if="items.length" class="list table">
      <div class="table-row table-head">
        <span>名称</span>
        <span v-if="showPostCount">文章数</span>
        <span>版本</span>
        <span class="text-right">操作</span>
      </div>

      <div v-for="item in items" :key="item.id" class="table-row">
        <template v-if="editingId === item.id">
          <input
            v-model="editingName"
            class="input"
            maxlength="100"
            @keyup.enter="saveEdit(item)"
            @keyup.esc="cancelEdit"
          />
          <span v-if="showPostCount" class="muted">{{ item.postCount }}</span>
          <span class="muted">v{{ item.version }}</span>
          <span class="text-right row-actions">
            <button class="link" :disabled="busy" @click="saveEdit(item)">保存</button>
            <button class="link link-muted" :disabled="busy" @click="cancelEdit">取消</button>
          </span>
        </template>

        <template v-else>
          <span class="name">{{ item.name }}</span>
          <span v-if="showPostCount" class="muted">{{ item.postCount }} 篇</span>
          <span class="muted">v{{ item.version }}</span>
          <span class="text-right row-actions">
            <button class="link" :disabled="busy" @click="startEdit(item)">编辑</button>
            <button class="link link-danger" :disabled="busy" @click="onDelete(item)">删除</button>
          </span>
        </template>
      </div>
    </div>

    <div v-else class="empty">还没有任何{{ label(false) }}</div>
  </section>
</template>

<style scoped>
.taxonomy {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.add-bar {
  display: flex;
  gap: var(--space-3);
  padding: var(--space-4);
  max-width: 560px;
}

/* 按钮不要被 flex 压缩变窄 */
.add-bar .btn {
  flex: 0 0 auto;
}

.list {
  overflow: hidden;
  max-width: 760px;
}

.table-row {
  grid-template-columns: 1fr 96px 72px 128px;
}

.name {
  color: var(--text-strong);
  font-weight: 600;
}

@media (max-width: 640px) {
  .table-head {
    display: none;
  }
  .table-row {
    grid-template-columns: 1fr;
    gap: var(--space-2);
  }
  .text-right {
    text-align: left;
  }
  .row-actions {
    justify-content: flex-start;
  }
}
</style>
