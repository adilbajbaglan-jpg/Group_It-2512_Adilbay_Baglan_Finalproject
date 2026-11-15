namespace LibraryDesk;

public sealed record DueRow(string Id, string Title, string Reader, DateOnly Due, int Days);
public sealed record PopularRow(string Id, string Title, int Count);

public static class Reports
{
    public static IEnumerable<Loan> LateLoans(IEnumerable<Loan> loans, DateOnly asOf)
    {
        foreach (var loan in loans)
            if (loan.State is LoanState.Active && loan.LastAction <= asOf && loan.Due < asOf)
                yield return loan;
    }

    public static List<DueRow> Overdue(Shelf shelf, DateOnly asOf) =>
        (from loan in LateLoans(shelf.Loans, asOf)
         join book in shelf.Books on loan.BookId equals book.Id
         select new DueRow(book.Id, book.Title, loan.Reader, loan.Due, asOf.DayNumber - loan.Due.DayNumber))
        .OrderByDescending(row => row.Days).ThenBy(row => row.Id, StringComparer.Ordinal).ToList();

    public static List<PopularRow> Popular(Shelf shelf) =>
        shelf.Loans.GroupBy(loan => loan.BookId)
            .Join(shelf.Books, group => group.Key, book => book.Id,
                (group, book) => new PopularRow(book.Id, book.Title, group.Count()))
            .OrderByDescending(row => row.Count).ThenBy(row => row.Id, StringComparer.Ordinal).ToList();
}
