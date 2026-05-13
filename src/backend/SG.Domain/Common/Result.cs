namespace SG.Domain.Common;

public record DomainError(string Code, string Message)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);
}

public class Result
{
    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && error != DomainError.None)
            throw new InvalidOperationException("Un resultado exitoso no puede tener error.");
        if (!isSuccess && error == DomainError.None)
            throw new InvalidOperationException("Un resultado fallido requiere un error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);
    public static Result Failure(DomainError error) => new(false, error);
    public static Result Failure(string code, string message) => new(false, new DomainError(code, message));

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, DomainError.None);
    public static Result<TValue> Failure<TValue>(DomainError error) => new(default, false, error);
    public static Result<TValue> Failure<TValue>(string code, string message) =>
        new(default, false, new DomainError(code, message));
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, DomainError error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");
}
