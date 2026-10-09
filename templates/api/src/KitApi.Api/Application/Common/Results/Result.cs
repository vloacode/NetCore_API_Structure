namespace KitApi.Application.Common.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(code, message, ErrorType.Validation, details);

    /// <summary>Convierte los errores de ASP.NET Identity en un Error de validación.</summary>
    public static Error FromIdentity(IEnumerable<Microsoft.AspNetCore.Identity.IdentityError> errors)
    {
        var details = errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Validation("Identity.Validation", "Uno o más errores de validación.", details);
    }
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException("Un resultado exitoso no puede tener error.");
        if (!isSuccess && error == Error.None) throw new InvalidOperationException("Un resultado fallido debe tener error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer Value de un resultado fallido.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
