using Blog.Application.Common;
using Blog.Application.Interfaces;
using Blog.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blog.WebApi.Controllers
{
    /// <summary>
    /// 版本信息：回答「线上现在跑的是哪一版」。
    ///
    /// <para><b>为什么需要它</b></para>
    /// 在这之前，判断线上版本只能靠 <c>docker inspect</c> 看镜像 tag、或者上服务器翻
    /// compose 文件 —— 排障时多绕好几步，而且 <c>:latest</c> 是会飘的，看它等于没看。
    /// 有了这个端点，一句话就能确认：
    /// <code>curl -s https://站点/api/version</code>
    ///
    /// <para><b>为什么匿名可访问</b></para>
    /// 它要能在负载均衡后面、在任何"还没登录"的排障场景下被问到。
    /// 版本号本身不是秘密（镜像 tag 在 registry 上也是公开的），
    /// 因此这里不加鉴权 —— 但**必须严格限制暴露的字段**（见 <see cref="BuildInfoDto"/>）：
    /// 只回版本 / commit / 构建时间，绝不含路径、连接串、环境变量。
    /// 这条约束由 <c>VersionEndpointTests</c> 钉住。
    /// </summary>
    [ApiController]
    [Route("api/version")]
    public class VersionController : ControllerBase
    {
        private readonly BuildInfoProvider _buildInfo;

        public VersionController(BuildInfoProvider buildInfo)
        {
            _buildInfo = buildInfo;
        }

        /// <summary>当前后端版本。无需登录。</summary>
        [HttpGet]
        [AllowAnonymous]
        public ApiResponse<BuildInfoDto> Get() => ApiResponse<BuildInfoDto>.Ok(_buildInfo.Get());
    }
}
