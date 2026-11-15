namespace LibraryDesk;

public abstract record Result<T>
{
    private Result() { }
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(string Reason) : Result<T>;

    public Result<U> Map<U>(Func<T, U> transform) => Bind(value => new Result<U>.Ok(transform(value)));
    public Result<U> Bind<U>(Func<T, Result<U>> next) => this switch
    {
        Ok(var value) => next(value),
        Fail(var reason) => new Result<U>.Fail(reason),
        _ => throw new InvalidOperationException()
    };
}

public abstract record Maybe<T>
{
    private Maybe() { }
    public sealed record Some(T Value) : Maybe<T>;
    public sealed record None : Maybe<T>;
}

public static class LookupExtensions
{
    public static Maybe<T> FindMaybe<T>(this IEnumerable<T> values, Func<T, bool> predicate)
    {
        foreach (var value in values)
            if (predicate(value)) return new Maybe<T>.Some(value);
        return new Maybe<T>.None();
    }

    public static Result<T> OrFail<T>(this Maybe<T> value, string reason) => value switch
    {
        Maybe<T>.Some(var found) => new Result<T>.Ok(found),
        _ => new Result<T>.Fail(reason)
    };
}
