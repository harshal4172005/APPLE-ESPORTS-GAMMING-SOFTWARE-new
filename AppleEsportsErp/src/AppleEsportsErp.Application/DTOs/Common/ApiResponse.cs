namespace AppleEsportsErp.Application.DTOs.Common;

/// <summary>Standard API response envelope — matches Node.js response format</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? Code { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error, string? code = null) => new() { Success = false, Error = error, Code = code };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Code { get; set; }
    public object? Details { get; set; }

    public static ApiResponse Ok() => new() { Success = true };
    public static ApiResponse Fail(string error, string? code = null) => new() { Success = false, Error = error, Code = code };
}
