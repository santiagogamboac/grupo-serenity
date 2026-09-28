namespace Serenity.Application.Common;

/// <summary>Clasifica un fallo esperado para que la capa HTTP elija el status correcto.</summary>
public enum ErrorTipo
{
    Validacion,
    NoAutorizado,
    Bloqueado,
    NoEncontrado,
    Conflicto
}

/// <summary>
/// Result pattern para commands que no devuelven datos: los fallos esperados no se
/// modelan con excepciones (igual que en urbanos), pero aquí además llevan un
/// <see cref="ErrorTipo"/> para no repetir el mapeo a HTTP en cada controller.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? error, ErrorTipo tipo)
    {
        IsSuccess = isSuccess;
        Error = error;
        Tipo = tipo;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public ErrorTipo Tipo { get; }

    public static Result Success() => new(true, null, ErrorTipo.Validacion);
    public static Result Failure(string error, ErrorTipo tipo = ErrorTipo.Validacion) => new(false, error, tipo);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, ErrorTipo tipo = ErrorTipo.Validacion) => Result<T>.Failure(error, tipo);
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? error, ErrorTipo tipo)
        : base(isSuccess, error, tipo)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null, ErrorTipo.Validacion);
    public new static Result<T> Failure(string error, ErrorTipo tipo = ErrorTipo.Validacion) => new(false, default, error, tipo);
}
