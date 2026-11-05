namespace Blog.Application.Common.Exceptions
{
    /// <summary>
    /// 第三方 provider 调用失败（网络、token 交换、用户信息解析等）。
    /// 消息只用于服务端日志/排障，不直接展示给用户。
    /// </summary>
    public sealed class ExternalAuthException : Exception
    {
        public ExternalAuthException(string message) : base(message)
        {
        }

        public ExternalAuthException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
