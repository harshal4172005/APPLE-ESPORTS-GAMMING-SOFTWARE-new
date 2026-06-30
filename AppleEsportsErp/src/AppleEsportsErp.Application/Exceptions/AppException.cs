using System.Net;

namespace AppleEsportsErp.Application.Exceptions;

/// <summary>Base application exception — maps from Node.js AppError</summary>
public class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Code { get; }

    public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string code = "APP_ERROR")
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

public class AuthenticationException : AppException
{
    public AuthenticationException(string message, string code = "AUTH_ERROR")
        : base(message, HttpStatusCode.Unauthorized, code) { }
}

public class AuthorizationException : AppException
{
    public AuthorizationException(string message, string code = "FORBIDDEN")
        : base(message, HttpStatusCode.Forbidden, code) { }
}

public class BranchIsolationException : AppException
{
    public BranchIsolationException(string message)
        : base(message, HttpStatusCode.Forbidden, "BRANCH_ISOLATION") { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(message, HttpStatusCode.NotFound, code) { }
}
