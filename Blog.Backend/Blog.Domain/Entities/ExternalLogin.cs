using Blog.Domain.Entities.Base;

namespace Blog.Domain.Entities
{
    /// <summary>第三方登录平台。数据库按字符串存储，便于以后扩展。</summary>
    public enum ExternalAuthProvider
    {
        GitHub = 0,
        Feishu = 1,
    }

    /// <summary>
    /// 本站账号与第三方身份的绑定关系。
    ///
    /// <para>只保存「登录证明」：平台 + 平台侧稳定用户 Id + 所属本站用户。
    /// 刻意不保存昵称、头像、邮箱、手机号或第三方 access_token /
    /// refresh_token —— 这些都不是登录必需，存了只会扩大泄露面。</para>
    ///
    /// <para>一个本站用户对同一个平台最多一条有效绑定；同一个第三方身份
    /// 也只能绑定到一个本站用户（两个唯一索引兜底）。解绑走软删除，
    /// 过滤唯一索引允许之后重新绑定。</para>
    /// </summary>
    public class ExternalLogin : BaseEntity
    {
        /// <summary>所属本站账号</summary>
        public Guid UserId { get; private set; }

        /// <summary>第三方平台</summary>
        public ExternalAuthProvider Provider { get; private set; }

        /// <summary>
        /// 平台侧稳定用户标识：
        /// GitHub = 数字 id（不是 login，login 可改名）；
        /// 飞书 = open_id（应用内唯一，不要删除重建应用）。
        /// </summary>
        public string ProviderUserId { get; private set; } = string.Empty;

        /// <summary>最近一次通过该第三方身份登录的时间（审计用）</summary>
        public DateTimeOffset? LastLoginAt { get; private set; }

        /// <summary>导航属性</summary>
        public User? User { get; set; }

        private ExternalLogin() { } // EF Core 需要无参构造函数

        public ExternalLogin(Guid userId, ExternalAuthProvider provider, string providerUserId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("用户 Id 不能为空", nameof(userId));
            if (string.IsNullOrWhiteSpace(providerUserId))
                throw new ArgumentException("第三方用户 Id 不能为空", nameof(providerUserId));

            UserId = userId;
            Provider = provider;
            ProviderUserId = providerUserId;
            CreatedAt = DateTimeOffset.UtcNow;
            IsDeleted = false;
        }

        /// <summary>第三方登录成功后记录时间</summary>
        public void MarkLoggedIn()
        {
            LastLoginAt = DateTimeOffset.UtcNow;
        }
    }
}
