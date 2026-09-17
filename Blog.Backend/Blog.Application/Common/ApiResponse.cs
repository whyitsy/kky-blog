namespace Blog.Application.Common
{
    /// <summary>
    /// 统一 API 响应体：{ "code": 0, "data": {}, "message": "ok" }
    ///
    /// <para><b>业务代码只应构造「成功」响应。</b></para>
    /// 失败响应一律 <c>throw new BusinessException(msg, code)</c>，由
    /// <c>ExceptionHandlingMiddleware</c> 统一转成 <see cref="ApiResponse"/> 并映射 HTTP 状态码。
    /// 为什么：<c>return ApiResponse.Fail(...)</c> 只能表达 body 里的 <c>code</c>，
    /// 拿到的是一个 **HTTP 200** —— 于是「参数不合法」在这一个端点是 200、在别处是 400，
    /// 客户端必须记得"这个接口只看 code"。
    ///
    /// <para><b>只有「框架钩子」才允许 return 失败响应</b>（见 docs/02-架构与数据模型.md §4.2）：</para>
    /// 那些地方**没有业务调用栈可抛**——中间件、<c>InvalidModelStateResponseFactory</c>、
    /// JwtBearer 的 <c>OnChallenge</c>/<c>OnForbidden</c>、限流中间件、状态码页兜底。
    /// 它们正是 <see cref="ApiResponse.Fail"/> 的调用者。
    /// </summary>
    public class ApiResponse<T>
    {
        public int Code { get; init; }
        public string Message { get; init; } = "ok";
        public T? Data { get; init; }

        public static ApiResponse<T> Ok(T data) => new() { Code = ErrorCodes.Ok, Data = data };

        // 这里**刻意没有** Fail：
        //
        // 原先存在一个 `public static ApiResponse<T> Fail(code, message, data = default)`，
        // 但实测引用数为 **0**（连 ExceptionHandlingMiddleware 都不用 —— 它走的是
        // 非泛型 ApiResponse.Fail，因为响应体的 T 要到运行时才知道）。
        // 一个没人调用的公开 API 只会制造"这里似乎也能返回错误"的错觉，
        // 于是被删掉，把「失败响应」收敛成 ApiResponse.Fail 一个入口。
    }

    public static class ApiResponse
    {
        /// <summary>无数据的成功响应</summary>
        public static ApiResponse<object?> Ok() => new() { Code = ErrorCodes.Ok };

        /// <summary>
        /// 失败响应的**唯一入口**。
        ///
        /// 调用者应当只有框架钩子（见类型注释）。业务代码请抛
        /// <see cref="Exceptions.BusinessException"/>，否则会得到 HTTP 200。
        /// </summary>
        public static ApiResponse<object?> Fail(int code, string message) =>
            new() { Code = code, Message = message };
    }
}
