using LibraryDesk;
using System.Diagnostics;
using System.Reflection;

if (args is ["--missing-native"])
    return NativeFees.Quote(1) is Result<int>.Fail f && f.Reason.StartsWith("Cannot load native fee function:") ? 0 : 1;

var start = new DateOnly(2026, 11, 1);
var book = Ok(Book.Create("b01", "The Hobbit"));
var shelf = Ok(Shelf.Create([book]));
var issued = Ok(shelf.Issue("B01", "Dana", start, Policies.Standard));
var due = start.AddDays(14);
var tests = new (string Name, Action Check)[]
{
    ("01 Book factory normalises IDs and titles", () =>
    {
        var item = Ok(Book.Create(" b07 ", " Dune "));
        Equal("B07", item.Id); Equal("Dune", item.Title);
    }),
    ("02 Invalid books and duplicate catalogue IDs fail", () =>
    {
        Bad(Book.Create(" ", "Story")); Bad(Book.Create("B", " ")); Bad(Book.Create("B 1", "Story"));
        Bad(Shelf.Create([book, book]));
    }),
    ("03 A caller-owned list cannot change the shelf", () =>
    {
        var list = new List<Book> { book };
        var original = Ok(Shelf.Create(list));
        list.Clear(); Equal(1, original.Books.Length); Equal(0, shelf.Loans.Length);
    }),
    ("04 Missing and successful lookups are explicit", () =>
    {
        Equal(true, shelf.FindBook("NONE") is Maybe<Book>.None);
        Equal(book, Ok(shelf.FindBook(" b01 ").OrFail("missing")));
    }),
    ("05 Standard and extended policies differ on the same input", () =>
    {
        var longer = Ok(shelf.Issue("B01", "Dana", start, Policies.Extended));
        Equal(due, issued.Loans[0].Due); Equal(start.AddDays(28), longer.Loans[0].Due);
    }),
    ("06 Empty reader and dates outside the range fail", () =>
    {
        Bad(shelf.Issue("B01", " ", start, Policies.Standard));
        Bad(Policies.Standard(DateOnly.MaxValue)); Bad(Policies.Period(0)(start));
        Equal(DateOnly.MaxValue, Ok(Policies.Standard(DateOnly.MaxValue.AddDays(-14))));
    }),
    ("07 Failure skips both Bind and Map", () =>
    {
        bool called = false;
        var result = new Result<int>.Fail("first").Bind<int>(v => { called = true; return new Result<int>.Ok(v); })
            .Map(v => { called = true; return v + 1; });
        Equal("first", Bad(result)); Equal(false, called);
        Bad(issued.Issue("B01", "Kai", start, day => throw new Exception("policy should be skipped")));
    }),
    ("08 Renewal works on the due date and only once", () =>
    {
        var next = Ok(issued.Renew("B01", due));
        Equal(due.AddDays(7), next.Loans[0].Due); Bad(next.Renew("B01", due));
        Bad(issued.Renew("B01", due.AddDays(1))); Bad(issued.Renew("B01", start.AddDays(-1)));
        Equal(due, issued.Loans[0].Due);
    }),
    ("09 Return rejects a backwards date before calling the fee", () =>
    {
        var next = Ok(issued.Renew("B01", due));
        Bad(next.Return("B01", start, _ => throw new Exception("fee should be skipped")));
        Bad(shelf.Renew("B01", start)); Bad(shelf.Return("B01", start, NativeFees.Quote));
    }),
    ("10 Return failure preserves the active loan", () =>
    {
        Bad(issued.Return("B01", due, _ => new Result<int>.Fail("integration unavailable")));
        Bad(issued.Return("B01", due, _ => new Result<int>.Ok(-1)));
        Equal(true, issued.Loans[0].State is LoanState.Active);
    }),
    ("11 Complete issue, renew and return produces a receipt", () =>
    {
        var renewed = Ok(issued.Renew("B01", due));
        var back = Ok(renewed.Return("B01", due.AddDays(10), NativeFees.Quote));
        var closed = (LoanState.Returned)back.Loans.Single().State;
        Equal(300, closed.FeeKzt); Equal(due.AddDays(10), closed.On);
        Bad(back.Return("B01", closed.On, NativeFees.Quote)); Bad(back.Renew("B01", closed.On));
    }),
    ("12 A returned copy can be reissued on the return date", () =>
    {
        var returned = Ok(issued.Return("B01", due, NativeFees.Quote));
        Bad(returned.Issue("B01", "Ali", due.AddDays(-1), Policies.Standard));
        var next = Ok(returned.Issue("B01", "Ali", due, Policies.Extended));
        Equal(2, next.Loans.Length); Equal(2, Reports.Popular(next).Single().Count);
        Equal(0, ((LoanState.Returned)next.Loans[0].State).FeeKzt);
    }),
    ("13 Empty reports and due-date boundaries", () =>
    {
        Equal(0, Reports.Popular(shelf).Count); Equal(0, Reports.Overdue(issued, due).Count);
        Equal(1, Reports.Overdue(issued, due.AddDays(1)).Single().Days);
    }),
    ("14 Both reports break ties by book ID", () =>
    {
        var b = Ok(Book.Create("B02", "Dune"));
        var first = Ok(Ok(Shelf.Create([b, book])).Issue(b.Id, "Ali", start, Policies.Standard));
        var both = Ok(first.Issue(book.Id, "Dana", start, Policies.Standard));
        Equal("B01,B02", string.Join(",", Reports.Popular(both).Select(r => r.Id)));
        Equal("B01,B02", string.Join(",", Reports.Overdue(both, due.AddDays(2)).Select(r => r.Id)));
    }),
    ("15 yield defers enumeration and a list snapshot is reusable", () =>
    {
        int visits = 0;
        IEnumerable<Loan> Source() { visits++; yield return issued.Loans[0]; }
        var stream = Reports.LateLoans(Source(), due.AddDays(1)); Equal(0, visits);
        var snapshot = stream.ToList(); Equal(1, visits);
        Equal(1, snapshot.Count); Equal(1, snapshot.ToArray().Length); Equal(1, visits);
    }),
    ("16 Native function handles zero, cap and maximum days", () =>
    {
        Equal(0, Ok(NativeFees.Quote(0))); Equal(2900, Ok(NativeFees.Quote(29)));
        Equal(3000, Ok(NativeFees.Quote(30))); Equal(3000, Ok(NativeFees.Quote(int.MaxValue)));
        Bad(NativeFees.Quote(-1));
    }),
    ("17 A missing native binary gives a controlled integration failure", () =>
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--missing-native");
        info.Environment["LIBRARYDESK_NATIVE"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing");
        using var child = Process.Start(info) ?? throw new Exception("Child process failed to start.");
        if (!child.WaitForExit(15000)) { child.Kill(true); throw new Exception("Native failure test timed out."); }
        Equal(0, child.ExitCode);
    }),
    ("18 Missing JSON file fails", () => InTemp(file => { Bad(Storage.Read(file)); Equal(false, File.Exists(file)); })),
    ("19 Malformed JSON fails and releases the file", () => InTemp(file =>
    {
        File.WriteAllText(file, "{bad"); Bad(Storage.Read(file));
        using var exclusive = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Equal(4L, exclusive.Length);
    })),
    ("20 Invalid persisted domain history and schema fail", () =>
    {
        Bad(Ledger.Restore(new DiskData(1, [new BookRow("B01","Story")], [new Entry("return","B01","2026-01-01")])));
        Bad(Ledger.Restore(new DiskData(2, [], []))); Bad(Ledger.Restore(new DiskData(1, [null], [])));
        Bad(Actions.Apply(shelf, new Entry("issue","B01","2026-02-30","Dana","standard"), NativeFees.Quote));
        Bad(Actions.Apply(shelf, new Entry("issue","B01","2026-11-01","Dana","unknown"), NativeFees.Quote));
    }),
    ("21 Failed edits leave bytes unchanged; successful edits reload", () => InTemp(file =>
    {
        File.WriteAllText(file, """{"version":1,"books":[{"id":"B01","title":"Story"}],"events":[]}""");
        var entry = new Entry("issue","B01","2026-11-01","Dana","standard");
        Ok(Storage.Update(file, entry)); string before = File.ReadAllText(file);
        Bad(Storage.Update(file, entry)); Equal(before, File.ReadAllText(file));
        Ok(Storage.Update(file, new Entry("return","B01","2026-11-18")));
        var closed = (LoanState.Returned)Ok(Storage.Read(file)).Current.Loans.Single().State;
        Equal(300, closed.FeeKzt);
    })),
    ("22 A competing writer cannot overwrite the current ledger", () => InTemp(file =>
    {
        const string json = """{"version":1,"books":[],"events":[]}""";
        File.WriteAllText(file, json);
        using var guard = File.Open(file + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        string error = Bad(Storage.Update(file, new Entry("renew","B01","2026-11-01")));
        Equal(true, error.StartsWith("Cannot update data:")); Equal(json, File.ReadAllText(file));
    })),
    ("23 A past report ignores a later return", () =>
    {
        var data = new DiskData(1, [new BookRow("B01","Story")],
            [new Entry("issue","B01","2026-11-01","Dana","standard"), new Entry("return","B01","2026-11-20")]);
        var ledger = Ok(Ledger.Restore(data));
        Equal(0, Reports.Overdue(ledger.Current, due.AddDays(2)).Count);
        Equal(2, Reports.Overdue(Ok(ledger.At(due.AddDays(2))), due.AddDays(2)).Single().Days);
    })
};

int passed = 0;
foreach (var (name, check) in tests)
{
    try { check(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception e) { Console.WriteLine("FAIL " + name + " | " + e.Message); }
}
Console.WriteLine($"{passed}/{tests.Length} passed.");
return passed == tests.Length ? 0 : 1;

static T Ok<T>(Result<T> r) => r is Result<T>.Ok success ? success.Value :
    throw new Exception("Unexpected failure: " + ((Result<T>.Fail)r).Reason);

static string Bad<T>(Result<T> r) => r is Result<T>.Fail fail ? fail.Reason : throw new Exception("Expected failure.");

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, received {actual}.");
}

static void InTemp(Action<string> body)
{
    string dir = Path.Combine(Path.GetTempPath(), "librarydesk-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try { body(Path.Combine(dir, "data.json")); }
    finally { Directory.Delete(dir, true); }
}
