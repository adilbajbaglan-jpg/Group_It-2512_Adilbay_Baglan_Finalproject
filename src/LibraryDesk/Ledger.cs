using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibraryDesk;

public sealed record BookRow(string? Id, string? Title);
public sealed record Entry(string? Action, string? Book, string? Date, string? Reader = null, string? Policy = null);
public sealed record DiskData(int Version, List<BookRow?>? Books, List<Entry?>? Events);

public static class Actions
{
    public static bool ReadDate(string? raw, out DateOnly day) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    public static Result<Shelf> Apply(Shelf shelf, Entry entry, Func<int, Result<int>> quote)
    {
        if (!ReadDate(entry.Date, out var on)) return new Result<Shelf>.Fail("Use a valid date in yyyy-MM-dd format.");
        if (entry.Action == "issue") return entry.Policy switch
        {
            "standard" => shelf.Issue(entry.Book, entry.Reader, on, Policies.Standard),
            "extended" => shelf.Issue(entry.Book, entry.Reader, on, Policies.Extended),
            _ => new Result<Shelf>.Fail("Policy must be standard or extended.")
        };
        if (entry.Reader is not null || entry.Policy is not null)
            return new Result<Shelf>.Fail("Only issue takes a reader and a policy.");
        return entry.Action switch
        {
            "renew" => shelf.Renew(entry.Book, on),
            "return" => shelf.Return(entry.Book, on, quote),
            _ => new Result<Shelf>.Fail("Unknown lending action.")
        };
    }
}

public sealed class Ledger
{
    public Shelf Current { get; }
    public ImmutableArray<Entry> Events { get; }
    private Ledger(Shelf current, ImmutableArray<Entry> events) => (Current, Events) = (current, events);

    public static Result<Ledger> Restore(DiskData? file)
    {
        if (file is not { Version: 1, Books: not null, Events: not null })
            return new Result<Ledger>.Fail("Expected version 1 with books and events arrays.");
        var books = new List<Book>();
        foreach (var row in file.Books)
        {
            var item = Book.Create(row?.Id, row?.Title);
            if (item is Result<Book>.Fail bad) return new Result<Ledger>.Fail(bad.Reason);
            books.Add(((Result<Book>.Ok)item).Value);
        }
        var result = Shelf.Create(books).Map(shelf => new Ledger(shelf, []));
        foreach (var entry in file.Events)
        {
            if (entry is null) return new Result<Ledger>.Fail("Invalid null event.");
            result = result.Bind(ledger => ledger.Append(entry));
            if (result is Result<Ledger>.Fail) return result;
        }
        return result;
    }

    public Result<Ledger> Append(Entry entry) =>
        Actions.Apply(Current, entry, NativeFees.Quote).Map(next => new Ledger(next, Events.Add(entry)));

    public Result<Shelf> At(DateOnly date)
    {
        var result = Shelf.Create(Current.Books);
        foreach (var entry in Events)
            if (Actions.ReadDate(entry.Date, out var on) && on <= date)
                result = result.Bind(shelf => Actions.Apply(shelf, entry, NativeFees.Quote));
        return result;
    }

    public DiskData ToDisk() => new(1,
        Current.Books.Select(book => (BookRow?)new BookRow(book.Id, book.Title)).ToList(),
        Events.Select(entry => (Entry?)entry).ToList());
}

public static class Storage
{
    private static readonly JsonSerializerOptions Format = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private static bool IsFileError(Exception e) =>
        e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException;

    public static Result<Ledger> Read(string path)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return Ledger.Restore(JsonSerializer.Deserialize<DiskData>(input, Format));
        }
        catch (Exception e) when (IsFileError(e)) { return new Result<Ledger>.Fail("Cannot read data: " + e.Message); }
    }

    public static Result<Ledger> Update(string path, Entry entry)
    {
        string? temporary = null;
        try
        {
            using var guard = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var next = Read(path).Bind(ledger => ledger.Append(entry));
            if (next is not Result<Ledger>.Ok updated) return next;
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, updated.Value.ToDisk(), Format);
                output.Flush(true);
            }
            File.Move(temporary, path, true);
            temporary = null;
            return next;
        }
        catch (Exception e) when (IsFileError(e)) { return new Result<Ledger>.Fail("Cannot update data: " + e.Message); }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
