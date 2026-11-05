<script setup lang="ts">
/**
 * 文章卡片骨架（首页 / 列表页）。
 * 尺寸与 PostCard 对齐（封面比例、正文内边距、行高），避免加载完成后的布局跳动。
 */
withDefaults(defineProps<{ count?: number }>(), { count: 12 })
</script>

<template>
  <div class="grid">
    <div v-for="i in count" :key="i" class="card sk-card">
      <div class="sk-cover shimmer" />
      <div class="sk-body">
        <div class="sk-line sk-title shimmer w-80" />
        <div class="sk-line shimmer w-100" />
        <div class="sk-line shimmer w-60" />
        <div class="sk-line sk-meta shimmer w-40" />
        <div class="sk-tags">
          <span class="sk-pill shimmer" />
          <span class="sk-pill shimmer" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-8);
}

.sk-card {
  overflow: hidden;
}

.sk-cover {
  aspect-ratio: 16 / 9;
  width: 100%;
}

/* 与 PostCard 的封面/正文衔接保持一致：正文上移压住封面、铺同一层渐变，
   否则加载完成时骨架和真实卡片的文字位置会跳一下。 */
.sk-body {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin: -64px 0 0;
  padding: 72px var(--space-5) var(--space-5);
  background: linear-gradient(
    180deg,
    transparent 0,
    color-mix(in srgb, var(--bg-surface) 35%, transparent) 22px,
    color-mix(in srgb, var(--bg-surface) 75%, transparent) 46px,
    var(--bg-surface) 64px
  );
}

.sk-title {
  height: 20px;
}

.sk-meta {
  height: 10px;
  margin-top: var(--space-1);
}

/* 标签行：两个小胶囊，和真实卡片的标签区高度对齐 */
.sk-tags {
  display: flex;
  gap: var(--space-2);
}

.sk-pill {
  width: 46px;
  height: 20px;
  border-radius: var(--radius-xs);
}

@media (max-width: 1199px) {
  .grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 767px) {
  .grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
