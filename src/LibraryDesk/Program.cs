using LibraryDesk;
using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
string path = Path.Combine("data", "library.json");
if (args.Length >= 2 && args[0] == "--data") { path = args[1]; args = args[2..]; }
return args.Length == 0 ? Menu() : Run(args, path);

int Menu()
{
    Help();
    while (true)
    {
        Console.Write("library> ");
        string? line = Console.ReadLine();
        if (line is null || line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) return 0;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0) Run(parts, path);
    }
}

int Run(string[] parts, string file)
{
    switch (parts)
    {
        case ["help"]: Help(); return 0;
        case ["demo"]: return Demo();
        case ["issue", var id, var reader, var day, var policy]:
            return Change(file, new Entry("issue", id, day, reader, policy));
        case [var action, var id, var day] when action is "renew" or "return":
            return Change(file, new Entry(action, id, day));
        case ["books"]:
            return Print(Storage.Read(file).Map(l => l.Current.Books.Select(b => b.Id + " | " + b.Title).ToList()));
        case ["popular"]:
            return Print(Storage.Read(file).Map(l => Reports.Popular(l.Current)
                .Select(r => $"{r.Id} | {r.Title} | issues {r.Count}").ToList()));
        case ["overdue", var day] when Actions.ReadDate(day, out var asOf):
            return Print(Storage.Read(file).Bind(l => l.At(asOf)).Map(s => Reports.Overdue(s, asOf)
                .Select(r => $"{r.Id} | {r.Reader} | due {r.Due:yyyy-MM-dd} | {r.Days} days late").ToList()));
        default: Console.Error.WriteLine("Use help for commands. Dates must use yyyy-MM-dd."); return 2;
    }
}

static int Change(string file, Entry entry) =>
    Print(Storage.Update(file, entry).Map(l =>
    {
        var loan = l.Current.Loans.Last(item => string.Equals(item.BookId, entry.Book, StringComparison.OrdinalIgnoreCase));
        string receipt = loan.State switch
        {
            LoanState.Returned(var on, var fee) => $"{loan.BookId} returned {on:yyyy-MM-dd} | fee {fee} KZT",
            _ => $"{loan.BookId} | {loan.Reader} | due {loan.Due:yyyy-MM-dd}"
        };
        return new List<string> { receipt };
    }));

static int Print(Result<List<string>> result)
{
    if (result is Result<List<string>>.Fail bad) { Console.Error.WriteLine("Rejected: " + bad.Reason); return 1; }
    var rows = ((Result<List<string>>.Ok)result).Value;
    if (rows.Count == 0) Console.WriteLine("No matching records.");
    foreach (var row in rows) Console.WriteLine(row);
    return 0;
}

int Demo()
{
    string dir = Path.Combine(Path.GetTempPath(), "librarydesk-" + Guid.NewGuid().ToString("N"));
    try
    {
        Directory.CreateDirectory(dir);
        string copy = Path.Combine(dir, "demo.json");
        File.Copy(Path.Combine("data", "sample.json"), copy);
        (string command, int expected)[] steps =
        [
            ("issue B03 Dana 2026-11-15 standard", 0), ("issue B03 Dana 2026-11-15 standard", 1),
            ("renew B03 2026-11-20", 0), ("return B03 2026-12-09", 0),
            ("overdue 2026-11-15", 0), ("popular", 0)
        ];
        foreach (var (command, expected) in steps)
        {
            Console.WriteLine("> " + command);
            if (Run(command.Split(' '), copy) != expected) return 1;
        }
        return 0;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine("Demo failed: " + e.Message); return 1;
    }
    finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
}

static void Help() => Console.WriteLine("""
    LibraryDesk | lending console
    books                             List the catalogue
    issue BOOK READER yyyy-MM-dd standard|extended
    renew BOOK yyyy-MM-dd             One renewal of 7 days
    return BOOK yyyy-MM-dd            Close the loan and calculate the fine
    overdue yyyy-MM-dd                Overdue loans as of that date
    popular                           Issue count by book, all dates
    demo                              Run a repeatable example on a temporary copy
    help / exit
    Example: issue B03 Dana 2026-11-15 standard
    """);
