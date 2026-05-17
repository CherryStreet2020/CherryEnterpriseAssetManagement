namespace Abs.FixedAssets.Models;

// ADR-014 D2 — Result<T> pattern for service-method-first action surface.
//
// Returned by every IXxxService method in Phase F. Both Razor page
// handlers AND the future voice-AI MCP tool layer call the same
// service method and consume the same Result shape.
//
// Use Result for expected failures (validation, business rule,
// permission denied). Exceptions for unexpected (DB down, network
// timeout). This matches the Milan Jovanović / Anton DevTips
// guidance and the Microsoft minimal-API pattern.
//
// Why a struct, not a class:
//   No allocations on the success path. Each service call returns
//   one struct on the stack — important when the voice layer fires
//   thousands of tool calls per session.
//
// Why public readonly record struct (not value-class):
//   Value semantics for free (equality, ToString), immutable by
//   construction, no boilerplate.
//
// Reference: ADR-014 §"Decisions" D2.
public readonly record struct Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public bool IsFailure => !IsSuccess;

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Non-generic helper class for type-inferred construction.
//   Result.Success(myValue)
//   Result.Failure<MyType>("nope")
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}
