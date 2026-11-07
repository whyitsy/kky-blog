<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { createCategory, getCategories } from '@/api/categories'
import { createTag, getTags } from '@/api/tags'
import { getCollections } from '@/api/collections'
import { importRemoteImage, uploadFile } from '@/api/files'
import { ACCEPT_IMAGE } from '@/utils/media'
import { buildImportPlan } from '@/utils/markdownImport'
import { localizeMarkdownImages } from '@/utils/markdownImageImport'
import { useAuthStore } from '@/stores/auth'
import type { CategoryDto, CollectionDto, PostDetailDto, PostPayload, TagDto } from '@/types'

/** 编辑器 props：
 *  - initial: 编辑模式下传入的 PostDetailDto；
 *  - mode: 'create' 隐藏 version 并显示发布开关；'edit' 显示版本号并禁用 publish。
 */
const props = defineProps<{
  initial?: PostDetailDto | null
  mode: 'create' | 'edit'
  saving?: boolean
}>()

const emit = defineEmits<{
  (e: 'save', payload: PostPayload): void
  (e: 'cancel'): void
}>()

const title = ref('')
const summary = ref('')
const content = ref('')
const coverImage = ref('')
const uploadingCover = ref(false)
const categoryId = ref<string | null>(null)
const selectedTagIds = ref<string[]>([])
const selectedCollectionIds = ref<string[]>([])
const publish = ref(true)

const auth = useAuthStore()

/**
 * 分类/标签的写接口仅 Admin 可用。
 * 作者也会用这个编辑器，因此「快速新建」只对管理员显示，
 * 避免出现「看得到、点了报 403」的死路。
 */
const canManageTaxonomy = computed(() => auth.isAdmin)

const categories = ref<CategoryDto[]>([])
const tags = ref<TagDto[]>([])
const collections = ref<CollectionDto[]>([])

// 内联新建分类/标签
const newCategoryName = ref('')
const newTagName = ref('')
const inlineError = ref('')

// .md 导入
const MD_IMPORT_MAX_BYTES = 5 * 1024 * 1024
const mdInput = ref<HTMLInputElement | null>(null)
const mdDirInput = ref<HTMLInputElement | null>(null)
const importingMd = ref(false)
const importingLocalImages = ref(false)
/** 导入过程中的副标题，如「本地化图片 2/7」；为空时按钮显示默认文案 */
const importStage = ref('')
/** 选择图片文件夹后的上传进度 */
const localizeStage = ref('')
const importNotice = ref('')
/** 正文里还没处理的本地图片；有值时才显示「选择图片文件夹 / 忽略」 */
const pendingLocalImages = ref<string[]>([])
const busyImporting = computed(() => importingMd.value || importingLocalImages.value)
const pendingLocalPreview = computed(() => {
  const names = pendingLocalImages.value.slice(0, 2).join('、')
  return pendingLocalImages.value.length > 2 ? `${names} 等` : names
})

const wordCount = computed(() => content.value.length)
const readMinutes = computed(() => Math.max(1, Math.round(wordCount.value / 400)))

function syncFromInitial() {
  if (!props.initial) {
    title.value = ''
    summary.value = ''
    content.value = ''
    coverImage.value = ''
    categoryId.value = null
    selectedTagIds.value = []
    selectedCollectionIds.value = []
    publish.value = true
    return
  }
  title.value = props.initial.title
  summary.value = props.initial.summary
  content.value = props.initial.content
  coverImage.value = props.initial.coverImage
  categoryId.value = props.initial.categoryId
  selectedTagIds.value = props.initial.tags.map((t) => t.id)
  // 一篇文章可属于多个专栏
  selectedCollectionIds.value = (props.initial.collections ?? []).map((c) => c.id)
}

watch(() => props.initial, syncFromInitial, { immediate: false })

async function loadTaxonomy() {
  // 专栏用 includeUnpublished=true：管理端/作者需要把文章挂到未发布专栏上
  const [cs, ts, cols] = await Promise.all([getCategories(), getTags(), getCollections(true).catch(() => [])])
  categories.value = cs
  tags.value = ts
  collections.value = cols
}

onMounted(() => {
  syncFromInitial()
  loadTaxonomy().catch(() => {
    inlineError.value = '加载分类/标签失败'
  })
})

const titleError = computed(() => {
  const t = title.value.trim()
  if (!t) return '标题不能为空'
  if (t.length > 200) return '标题长度不能超过 200'
  return ''
})
const contentError = computed(() => (content.value.trim() ? '' : '内容不能为空'))
const formValid = computed(() => !titleError.value && !contentError.value)

/**
 * 封面上传。刻意**不提供 URL 输入框**：封面地址会被直接放进 <img src>，
 * 允许手填就等于允许外部链接（访客 IP 泄露）与 javascript: / data: 之类的注入。
 * 服务端对 coverImage 也做 MediaPath 白名单校验，两层一致。
 */
async function onPickCover(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  if (!file.type.startsWith('image/')) {
    inlineError.value = '请选择图片文件'
    input.value = ''
    return
  }
  uploadingCover.value = true
  inlineError.value = ''
  try {
    coverImage.value = await uploadFile(file)
  } catch (e) {
    inlineError.value = e instanceof Error ? e.message : '封面上传失败'
  } finally {
    uploadingCover.value = false
    input.value = ''
  }
}

async function addCategory() {
  const name = newCategoryName.value.trim()
  if (!name) return
  try {
    const created = await createCategory(name)
    categories.value = [...categories.value, created].sort((a, b) => a.name.localeCompare(b.name))
    categoryId.value = created.id
    newCategoryName.value = ''
  } catch (e) {
    inlineError.value = e instanceof Error ? e.message : '新建分类失败'
  }
}

async function addTag() {
  const name = newTagName.value.trim()
  if (!name) return
  if (tags.value.some((t) => t.name === name)) {
    inlineError.value = '标签已存在'
    return
  }
  try {
    const created = await createTag(name)
    tags.value = [...tags.value, created].sort((a, b) => a.name.localeCompare(b.name))
    selectedTagIds.value = [...selectedTagIds.value, created.id]
    newTagName.value = ''
  } catch (e) {
    inlineError.value = e instanceof Error ? e.message : '新建标签失败'
  }
}

function toggleTag(id: string) {
  if (selectedTagIds.value.includes(id)) {
    selectedTagIds.value = selectedTagIds.value.filter((t) => t !== id)
  } else {
    selectedTagIds.value = [...selectedTagIds.value, id]
  }
}

function toggleCollection(id: string) {
  if (selectedCollectionIds.value.includes(id)) {
    selectedCollectionIds.value = selectedCollectionIds.value.filter((c) => c !== id)
  } else {
    selectedCollectionIds.value = [...selectedCollectionIds.value, id]
  }
}

function pickMarkdown() {
  mdInput.value?.click()
}

/**
 * 「选择图片文件夹」：只有正文里检测到本地相对路径图片时才出现。
 * 目录选择会保留 `webkitRelativePath`，所以 `article.md` + `images/` 的结构也能匹配；
 * 用户不选就保持原链接，等同于「忽略」。
 */
function pickImageFolder() {
  mdDirInput.value?.click()
}

async function onPickMarkdown(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = '' // 允许连续选择同一个文件
  if (file) await importMarkdownFile(file)
}

async function onPickImageFolder(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (files.length > 0) await uploadLocalImages(files)
}

async function importMarkdownFile(file: File) {
  if (busyImporting.value) return

  inlineError.value = ''
  importStage.value = ''

  if (!/\.(md|markdown)$/i.test(file.name)) {
    inlineError.value = '请选择 .md / .markdown 文件'
    return
  }
  if (file.size > MD_IMPORT_MAX_BYTES) {
    inlineError.value = '文件超过 5MB，请拆分后再导入'
    return
  }

  importingMd.value = true
  try {
    const text = await file.text()
    const plan = buildImportPlan({
      fileName: file.name,
      text,
      categories: categories.value,
      tags: tags.value,
    })

    if (
      props.mode === 'edit' &&
      content.value.trim() &&
      !window.confirm('导入会覆盖当前正文（以及文件里写明的标题、摘要等），继续？')
    ) {
      return
    }

    // confirm 之后再清状态：用户取消时，上一轮导入的提示和本地图片入口不能被清掉
    importNotice.value = ''
    pendingLocalImages.value = []

    const notices: string[] = []

    // 管理员：front matter 里写了但库里不存在的标签/分类，自动创建；作者没有权限，忽略即可
    const createdTags: TagDto[] = []
    const createdCategories: CategoryDto[] = []
    if (auth.isAdmin && plan.missingTags.length > 0) {
      for (const name of plan.missingTags) {
        try {
          createdTags.push(await createTag(name))
        } catch {
          notices.push(`跳过标签「${name}」`)
        }
      }
      if (createdTags.length > 0) {
        tags.value = [...tags.value, ...createdTags].sort((a, b) => a.name.localeCompare(b.name))
        notices.push(`新建标签 ${createdTags.length} 个`)
      }
    } else if (plan.missingTags.length > 0) {
      notices.push(`忽略不存在的标签：${plan.missingTags.join('、')}`)
    }

    if (auth.isAdmin && plan.missingCategories.length > 0) {
      for (const name of plan.missingCategories) {
        try {
          createdCategories.push(await createCategory(name))
        } catch {
          notices.push(`跳过分类「${name}」`)
        }
      }
      if (createdCategories.length > 0) {
        categories.value = [...categories.value, ...createdCategories].sort((a, b) => a.name.localeCompare(b.name))
        notices.push(`新建分类 ${createdCategories.length} 个`)
      }
    } else if (plan.missingCategories.length > 0) {
      notices.push(`忽略不存在的分类：${plan.missingCategories.join('、')}`)
    }

    if (plan.title !== undefined) title.value = plan.title
    if (plan.summary !== undefined) summary.value = plan.summary
    content.value = plan.content
    if (plan.coverImage !== undefined) coverImage.value = plan.coverImage
    if (plan.publish !== undefined) publish.value = plan.publish

    if (plan.tagIds !== undefined) {
      selectedTagIds.value = [...new Set([...plan.tagIds, ...createdTags.map((tag) => tag.id)])]
      if (plan.tagIds.length > 0) notices.push(`匹配标签 ${plan.tagIds.length} 个`)
    }

    if (plan.categoryId !== undefined) {
      categoryId.value = plan.categoryId
      notices.push('已匹配分类')
    } else if (createdCategories.length > 0) {
      categoryId.value = createdCategories[0]!.id
      notices.push('已选择新建的分类')
    }

    if (!plan.content.trim()) notices.push('文件里没有正文内容')

    // 在线图片（语雀 CDN / 站外）在这里自动处理；本地图片先不动，
    // 只有用户在下面的提示里主动选择图片文件夹才会上传，否则保留原链接（忽略）。
    try {
      const localized = await localizeMarkdownImages(plan.content, {
        files: [],
        importRemote: importRemoteImage,
        uploadLocal: uploadFile,
        onProgress: (done, total) => {
          importStage.value = `本地化图片 ${done}/${total}`
        },
      })

      content.value = localized.content
      pendingLocalImages.value = localized.missingLocal
      if (localized.localized > 0) notices.push(`本地化在线图片 ${localized.localized} 张`)
      if (localized.failures.length > 0) {
        notices.push(
          `图片失败 ${localized.failures.length} 张（保留原链接）：${summarizeImageFailures(localized.failures)}`,
        )
      }
    } catch (e) {
      // 编排层理论上不抛（单图异常已内部消化）；真抛了也只提示，正文已经导入
      notices.push(`图片本地化中断（${e instanceof Error ? e.message : '未知错误'}）`)
    }

    importNotice.value = notices.length > 0 ? `已导入：${notices.join('；')}` : '已导入正文'
  } catch (e) {
    inlineError.value = e instanceof Error ? `Markdown 导入失败：${e.message}` : 'Markdown 导入失败'
  } finally {
    importingMd.value = false
    importStage.value = ''
  }
}

/**
 * 用户选择了图片文件夹：重新扫一遍当前正文，把本地相对路径图片上传并改写。
 * 已经是 `/api/files/` 的图片会被忽略，所以这一步是幂等的；之前抓取失败的
 * 在线图片也会顺带重试一次（在线图片始终处理）。
 */
async function uploadLocalImages(files: File[]) {
  if (importingLocalImages.value || importingMd.value) return

  inlineError.value = ''
  localizeStage.value = ''
  importingLocalImages.value = true
  try {
    const localized = await localizeMarkdownImages(content.value, {
      files,
      importRemote: importRemoteImage,
      uploadLocal: uploadFile,
      onProgress: (done, total) => {
        localizeStage.value = `处理图片 ${done}/${total}`
      },
    })

    content.value = localized.content
    pendingLocalImages.value = localized.missingLocal

    const notices: string[] = []
    if (localized.localized > 0) notices.push(`本地化图片 ${localized.localized} 张`)
    if (localized.failures.length > 0) {
      notices.push(
        `图片失败 ${localized.failures.length} 张（保留原链接）：${summarizeImageFailures(localized.failures)}`,
      )
    }
    if (localized.missingLocal.length > 0) {
      notices.push(`还有 ${localized.missingLocal.length} 张本地图片没匹配到，请确认文件夹是否选对`)
    }
    if (notices.length === 0) notices.push('没有找到可上传的图片，请确认文件夹是否选对')

    const summary = `图片处理：${notices.join('；')}`
    importNotice.value = importNotice.value ? `${importNotice.value}；${summary}` : summary
  } catch (e) {
    inlineError.value = e instanceof Error ? `图片上传失败：${e.message}` : '图片上传失败'
  } finally {
    importingLocalImages.value = false
    localizeStage.value = ''
  }
}

function dismissLocalImages() {
  const count = pendingLocalImages.value.length
  pendingLocalImages.value = []
  if (count === 0) return

  const summary = `已忽略 ${count} 张本地图片（保留原链接）`
  importNotice.value = importNotice.value ? `${importNotice.value}；${summary}` : summary
}

function summarizeImageFailures(failures: Array<{ url: string; reason: string }>): string {
  const preview = failures
    .slice(0, 2)
    .map((failure) => `${failure.url}（${failure.reason}）`)
    .join('；')
  return failures.length > 2 ? `${preview} 等` : preview
}

function submit() {
  // 图片本地化还没结束就提交，会把「未完成上传的链接」存进库；
  // 按钮已经 disabled，这里再挡一次键盘 Enter 触发的提交。
  if (!formValid.value || busyImporting.value) return
  const payload: PostPayload = {
    title: title.value.trim(),
    summary: summary.value.trim(),
    content: content.value,
    coverImage: coverImage.value.trim(),
    categoryId: categoryId.value,
    tagIds: [...selectedTagIds.value],
    collectionIds: [...selectedCollectionIds.value],
    publish: props.mode === 'create' ? publish.value : undefined,
    version: props.initial?.version,
  }
  emit('save', payload)
}
</script>

<template>
  <form class="post-editor panel panel-stack" @submit.prevent="submit">
    <header class="pe-head">
      <h2>{{ mode === 'create' ? '新建文章' : '编辑文章' }}</h2>
      <p v-if="mode === 'edit' && initial" class="hint pe-meta">
        版本 v{{ initial.version }} · 最近更新 {{ initial.updatedAt?.replace('T', ' ').slice(0, 16) }}
      </p>
    </header>

    <label class="field">
      <span class="field-label">标题 <em class="required">*</em></span>
      <input v-model="title" class="input" type="text" maxlength="200" placeholder="给文章起一个清晰的标题" />
      <small v-if="titleError" class="hint-error">{{ titleError }}</small>
      <small v-else class="hint">{{ title.length }} / 200</small>
    </label>

    <label class="field">
      <span class="field-label">摘要</span>
      <!-- maxlength 与字数显示都是必须的：
           以前这里是 rows="2" 且两者皆无，粘贴长文本时只露两行，
           用户根本看不出真实长度，结果一路提交到数据库才报错。
           上限 200 与后端 FieldLimits.PostSummary / Posts.Summary 列宽保持一致。 -->
      <textarea
        v-model="summary"
        class="textarea"
        rows="3"
        maxlength="200"
        placeholder="可选：留空将自动取正文前 50 字"
      />
      <small class="hint">已填 {{ summary.length }} / 200 · 留空则自动取正文前 50 字</small>
    </label>

    <div class="field">
      <span class="field-label">封面图</span>
      <div class="media-picker">
        <div class="media-preview" :class="{ empty: !coverImage }">
          <img v-if="coverImage" :src="coverImage" alt="封面预览" />
          <span v-else>未设置</span>
        </div>
        <div class="media-picker-actions">
          <div class="media-picker-buttons">
            <label class="btn btn-sm">
              {{ uploadingCover ? '上传中...' : coverImage ? '更换封面' : '上传封面' }}
              <input type="file" :accept="ACCEPT_IMAGE" hidden :disabled="uploadingCover" @change="onPickCover" />
            </label>
            <button
              v-if="coverImage"
              type="button"
              class="btn btn-sm btn-ghost"
              :disabled="uploadingCover"
              @click="coverImage = ''"
            >
              移除封面
            </button>
          </div>
          <small class="hint">封面只能上传设置；不设置时列表卡片使用内置渐变占位</small>
        </div>
      </div>
    </div>

    <div class="row">
      <label class="field">
        <span class="field-label">分类</span>
        <select v-model="categoryId" class="select">
          <option :value="null">未分类</option>
          <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </label>

      <div v-if="canManageTaxonomy" class="field">
        <span class="field-label">快速新建分类</span>
        <div class="form-actions">
          <input v-model="newCategoryName" class="input" type="text" placeholder="分类名" @keyup.enter="addCategory" />
          <button type="button" class="btn btn-sm" :disabled="!newCategoryName.trim()" @click="addCategory">添加</button>
        </div>
      </div>
    </div>

    <div class="field">
      <span class="field-label">标签（多选）</span>
      <div class="chip-pool">
        <button
          v-for="t in tags"
          :key="t.id"
          type="button"
          class="chip"
          :class="{ active: selectedTagIds.includes(t.id) }"
          @click="toggleTag(t.id)"
        >
          {{ t.name }}
        </button>
        <span v-if="!tags.length" class="hint">
          {{ canManageTaxonomy ? '还没有标签，下方新建' : '还没有标签，请联系管理员创建' }}
        </span>
      </div>
      <!-- 与「快速新建分类」保持一致的可见标签，避免只有占位符导致的可发现性问题 -->
      <div v-if="canManageTaxonomy" class="inline-add">
        <span class="hint">快速新建标签</span>
        <input v-model="newTagName" class="input" type="text" placeholder="新标签名" @keyup.enter="addTag" />
        <button type="button" class="btn btn-sm" :disabled="!newTagName.trim()" @click="addTag">添加</button>
      </div>
    </div>

    <!-- 专栏（多选）：一篇文章可属于多个专栏。专栏是内容组织者，作者也可挂载 -->
    <div class="field">
      <span class="field-label">所属专栏（可多选）</span>
      <div class="chip-pool">
        <button
          v-for="c in collections"
          :key="c.id"
          type="button"
          class="chip"
          :class="{ active: selectedCollectionIds.includes(c.id) }"
          @click="toggleCollection(c.id)"
        >
          {{ c.title }}
        </button>
        <span v-if="!collections.length" class="hint">
          还没有专栏，请管理员在「专栏管理」中创建
        </span>
      </div>
    </div>

    <div class="field">
      <div class="field-head">
        <span class="field-label">正文 <em class="required">*</em> · Markdown · 约 {{ readMinutes }} 分钟阅读</span>
        <button type="button" class="btn btn-sm" :disabled="busyImporting" @click="pickMarkdown">
          {{ importStage || (importingMd ? '导入中...' : '从 .md 导入') }}
        </button>
      </div>
      <!-- 只选 .md；本地图片在导入后检测到才提示选文件夹，避免两个入口语义混淆 -->
      <input ref="mdInput" type="file" accept=".md,.markdown,text/markdown" hidden @change="onPickMarkdown" />
      <textarea v-model="content" class="textarea content-input" rows="14" placeholder="支持 Markdown：## 标题、- 列表、```代码块``` 等" />
      <small v-if="contentError" class="hint-error">{{ contentError }}</small>
      <small v-else class="hint">{{ wordCount }} 字</small>
      <small class="hint">
        支持 front matter：title / summary / description / tags / category / cover / draft；
        管理员导入时会自动创建缺失的标签与分类
      </small>
      <small class="hint">
        正文里的在线图片（语雀 CDN、站外）会自动下载并转存到本站；本地相对路径图片在导入后可选文件夹上传
      </small>
      <small v-if="importNotice" class="hint">{{ importNotice }}</small>

      <!-- 只有正文里确实存在本地相对路径图片时才出现。不选/忽略 = 保留原链接 -->
      <div v-if="pendingLocalImages.length > 0" class="banner banner-info local-images">
        <span>
          检测到 {{ pendingLocalImages.length }} 张本地图片（{{ pendingLocalPreview }}）。
          选择图片所在文件夹会自动上传；不选择则保留原链接（忽略）。
        </span>
        <button type="button" class="btn btn-sm" :disabled="busyImporting" @click="pickImageFolder">
          {{ importingLocalImages ? localizeStage || '上传中...' : '选择图片文件夹' }}
        </button>
        <button type="button" class="btn btn-sm btn-ghost" :disabled="busyImporting" @click="dismissLocalImages">
          忽略
        </button>
      </div>
      <!-- 目录选择保留 webkitRelativePath，article.md + images/ 的结构才能匹配 -->
      <input ref="mdDirInput" type="file" webkitdirectory hidden @change="onPickImageFolder" />
    </div>

    <label v-if="mode === 'create'" class="switch">
      <input v-model="publish" type="checkbox" />
      <span>创建后立即发布（未勾选则为草稿）</span>
    </label>

    <p v-if="inlineError" class="banner banner-error">{{ inlineError }}</p>

    <footer class="panel-actions">
      <button type="button" class="btn btn-ghost" :disabled="saving" @click="emit('cancel')">取消</button>
      <button type="submit" class="btn btn-primary" :disabled="!formValid || saving || uploadingCover || busyImporting">
        {{ saving ? '保存中...' : mode === 'create' ? '创建' : '保存修改' }}
      </button>
    </footer>
  </form>
</template>

<style scoped>
.post-editor {
  max-width: 880px;
  margin: 0 auto;
}

.pe-head h2 {
  font: var(--text-h2);
}

.panel-actions {
  margin-top: 0;
}

.field-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.local-images {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.local-images span {
  flex: 1 1 260px;
}

.content-input {
  font-family: var(--font-mono);
  line-height: 1.6;
}

.row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-4);
}
@media (max-width: 640px) {
  .row {
    grid-template-columns: 1fr;
  }
}
</style>
