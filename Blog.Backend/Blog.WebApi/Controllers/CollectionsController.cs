using Blog.Application.Common;
using Blog.Application.Common.Exceptions;
using Blog.Application.Interfaces;
using Blog.Application.Services.Collection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blog.WebApi.Controllers
{
    /// <summary>
    /// 专栏：把多篇文章组织成一个系列。
    /// 读接口公开（前台专栏页），写接口仅管理员（与分类/标签一致）。
    /// </summary>
    [ApiController]
    [Route("api/collections")]
    public class CollectionsController : ControllerBase
    {
        private readonly ICollectionService _collections;
        private readonly ICurrentUser _currentUser;

        public CollectionsController(ICollectionService collections, ICurrentUser currentUser)
        {
            _collections = collections;
            _currentUser = currentUser;
        }

        /// <summary>
        /// 专栏列表。前台默认只返回已发布；
        /// includeUnpublished=true 需 **Admin**（避免未发布专栏被匿名看到）。
        /// </summary>
        [HttpGet]
        public async Task<ApiResponse<List<CollectionDto>>> GetAll(
            [FromQuery] bool includeUnpublished = false,
            CancellationToken cancellationToken = default)
        {
            if (includeUnpublished)
                RequireAdmin();

            var items = await _collections.GetAllAsync(includeUnpublished, cancellationToken);
            return ApiResponse<List<CollectionDto>>.Ok(items);
        }

        /// <summary>专栏详情（含该专栏下已发布文章，按专栏内排序）</summary>
        [HttpGet("{slug}")]
        public async Task<ApiResponse<CollectionDetailDto>> GetBySlug(string slug, CancellationToken cancellationToken)
        {
            var detail = await _collections.GetBySlugAsync(slug, cancellationToken)
                ?? throw new BusinessException("专栏不存在", ErrorCodes.NotFound);

            // 未发布的专栏对外表现为不存在，避免通过状态码探测
            if (!detail.IsPublished && !IsAdmin())
                throw new BusinessException("专栏不存在", ErrorCodes.NotFound);

            return ApiResponse<CollectionDetailDto>.Ok(detail);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<CollectionDto>> Create(
            [FromBody] CreateCollectionRequest request, CancellationToken cancellationToken)
        {
            var created = await _collections.CreateAsync(request, cancellationToken);
            return ApiResponse<CollectionDto>.Ok(created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<CollectionDto>> Update(
            Guid id, [FromBody] UpdateCollectionRequest request, CancellationToken cancellationToken)
        {
            var updated = await _collections.UpdateAsync(id, request, cancellationToken);
            return ApiResponse<CollectionDto>.Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<object?>> Delete(
            Guid id, [FromQuery] int version, CancellationToken cancellationToken)
        {
            await _collections.DeleteAsync(id, version, cancellationToken);
            return ApiResponse.Ok();
        }

        /// <summary>整体设置专栏内的文章与顺序（多对多关系的权威入口）</summary>
        [HttpPut("{id:guid}/posts")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<CollectionDetailDto>> SetPosts(
            Guid id, [FromBody] SetCollectionPostsRequest request, CancellationToken cancellationToken)
        {
            var detail = await _collections.SetPostsAsync(id, request, cancellationToken);
            return ApiResponse<CollectionDetailDto>.Ok(detail);
        }

        /// <summary>
        /// 管理端按 id 取详情（含未发布文章与未发布专栏）。
        /// 与按 slug 的公开接口分开，避免把「未发布」的可见性判断混在公开路径里。
        /// </summary>
        [HttpGet("id/{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ApiResponse<CollectionDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var detail = await _collections.GetByIdAsync(id, cancellationToken)
                ?? throw new BusinessException("专栏不存在", ErrorCodes.NotFound);

            return ApiResponse<CollectionDetailDto>.Ok(detail);
        }

        // ---------------------------------------------------------------- 内部小工具

        /// <summary>
        /// 是否管理员 —— 读的是 <see cref="ICurrentUser"/>（身份解析结果），
        /// **不是** <c>User.IsInRole(...)</c>。
        ///
        /// <para>这两条专栏读接口是匿名可访问的，不走 <c>[Authorize]</c>，因此也就不会经过
        /// <c>RequireResolvedUserRequirement</c>。若直接读 JWT claim，一个「已注销 / 已停用
        /// 但尚未过期」的 Admin token 会被继续当作管理员，从而列出并读到未发布专栏
        /// （claims 里的 TokenVersion 校验结果只有解析中间件知道）。</para>
        /// </summary>
        private bool IsAdmin() => _currentUser.IsAdmin;

        private void RequireAdmin()
        {
            if (!IsAdmin())
                throw new BusinessException("无权查看未发布专栏", ErrorCodes.Forbidden);
        }
    }
}
