using System.Collections.Immutable;

namespace LibraryDesk;

public sealed class Book
{
    public string Id { get; }
    public string Title { get; }
    private Book(string id, string title) => (Id, Title) = (id, title);

    public static Result<Book> Create(string? id, string? title)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Trim().Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(title))
            return new Result<Book>.Fail("A book needs a single-word ID and a title.");
        return new Result<Book>.Ok(new Book(id.Trim().ToUpperInvariant(), title.Trim()));
    }
}

public abstract record LoanState
{
    private LoanState() { }
    public sealed record Active : LoanState;
    public sealed record Returned(DateOnly On, int FeeKzt) : LoanState;
}

public sealed class Loan
{
    public string BookId { get; }
    public string Reader { get; }
    public DateOnly IssuedOn { get; }
    public DateOnly Due { get; }
    public DateOnly LastAction { get; }
    public bool Renewed { get; }
    public LoanState State { get; }

    private Loan(string bookId, string reader, DateOnly issuedOn, DateOnly due,
        DateOnly lastAction, bool renewed, LoanState state)
    {
        (BookId, Reader, IssuedOn, Due) = (bookId, reader, issuedOn, due);
        (LastAction, Renewed, State) = (lastAction, renewed, state);
    }

    internal static Result<Loan> Open(Book book, string? reader, DateOnly on, DateOnly due)
    {
        if (string.IsNullOrWhiteSpace(reader) || reader.Trim().Any(char.IsWhiteSpace))
            return new Result<Loan>.Fail("Use a single-word reader ID.");
        if (due <= on) return new Result<Loan>.Fail("Due date must follow issue date.");
        return new Result<Loan>.Ok(new Loan(book.Id, reader.Trim(), on, due, on, false, new LoanState.Active()));
    }

    internal Result<Loan> Renew(DateOnly on)
    {
        if (State is not LoanState.Active) return new Result<Loan>.Fail("Loan is already closed.");
        if (on < LastAction || on > Due) return new Result<Loan>.Fail("Renewal must be between the last action and the due date.");
        if (Renewed) return new Result<Loan>.Fail("The renewal has already been used.");
        return Policies.Extension(Due).Map(due => new Loan(BookId, Reader, IssuedOn, due, on, true, State));
    }

    internal Result<Loan> Return(DateOnly on, Func<int, Result<int>> quote)
    {
        if (State is not LoanState.Active) return new Result<Loan>.Fail("Loan is already closed.");
        if (on < LastAction) return new Result<Loan>.Fail("Return cannot precede the last action.");
        int lateDays = Math.Max(0, on.DayNumber - Due.DayNumber);
        return quote(lateDays).Bind<Loan>(fee => fee < 0
            ? new Result<Loan>.Fail("Fee cannot be negative.")
            : new Result<Loan>.Ok(new Loan(BookId, Reader, IssuedOn, Due, on, Renewed, new LoanState.Returned(on, fee))));
    }
}

public static class Policies
{
    public static Func<DateOnly, Result<DateOnly>> Period(int days) => start =>
    {
        if (days <= 0 || days > DateOnly.MaxValue.DayNumber - start.DayNumber)
            return new Result<DateOnly>.Fail("Loan period is outside the supported date range.");
        return new Result<DateOnly>.Ok(start.AddDays(days));
    };

    public static readonly Func<DateOnly, Result<DateOnly>> Standard = Period(14);
    public static readonly Func<DateOnly, Result<DateOnly>> Extended = Period(28);
    public static readonly Func<DateOnly, Result<DateOnly>> Extension = Period(7);
}

public sealed class Shelf
{
    public ImmutableArray<Book> Books { get; }
    public ImmutableArray<Loan> Loans { get; }
    private Shelf(ImmutableArray<Book> books, ImmutableArray<Loan> loans) => (Books, Loans) = (books, loans);

    public static Result<Shelf> Create(IEnumerable<Book> books)
    {
        var catalogue = books.ToImmutableArray();
        if (catalogue.Select(book => book.Id).Distinct().Count() != catalogue.Length)
            return new Result<Shelf>.Fail("Duplicate book ID.");
        return new Result<Shelf>.Ok(new Shelf(catalogue, []));
    }

    public Maybe<Book> FindBook(string? id) =>
        Books.FindMaybe(book => string.Equals(book.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

    public Result<Shelf> Issue(string? id, string? reader, DateOnly on, Func<DateOnly, Result<DateOnly>> policy) =>
        FindBook(id).OrFail("Unknown book.")
            .Bind(book => CheckAvailability(book, on))
            .Bind(book => policy(on).Bind(due => Loan.Open(book, reader, on, due)))
            .Map(AddLoan);

    private Result<Book> CheckAvailability(Book book, DateOnly on)
    {
        var last = Loans.LastOrDefault(loan => loan.BookId == book.Id);
        return last?.State switch
        {
            LoanState.Active => new Result<Book>.Fail("Book is already on loan."),
            LoanState.Returned(var back, _) when on < back => new Result<Book>.Fail("Issue cannot precede the last return."),
            _ => new Result<Book>.Ok(book)
        };
    }

    private Shelf AddLoan(Loan loan) => new(Books, Loans.Add(loan));
    public Result<Shelf> Renew(string? id, DateOnly on) => ChangeLatest(id, loan => loan.Renew(on));
    public Result<Shelf> Return(string? id, DateOnly on, Func<int, Result<int>> quote) =>
        ChangeLatest(id, loan => loan.Return(on, quote));

    private Result<Shelf> ChangeLatest(string? id, Func<Loan, Result<Loan>> transition)
    {
        var loan = Loans.Reverse().FindMaybe(l => string.Equals(l.BookId, id?.Trim(), StringComparison.OrdinalIgnoreCase));
        return loan.OrFail("No loan exists for this book.")
            .Bind(previous => transition(previous).Map(next => new Shelf(Books, Loans.Replace(previous, next))));
    }
}
