# LibraryDesk

A librarian can issue one physical copy, renew it once and accept a return with a capped overdue fee. The catalogue and the history of lending actions live in one local JSON file. The package targets one student and provides three workflows, two reports and a real C library.

## Start on Windows

Use Windows x64 and the .NET SDK 8.0 (verified SDK 8.0.419 on Linux x64). The SDK includes the compiler and the runtime. Run `dotnet --list-sdks` to see available SDKs. Extract the entire archive first. Double-click **Start.cmd**. It makes a working copy of the sample on first use. Enter `demo` at the prompt for a repeatable walkthrough. Enter `exit` to leave. Double-click **Tests.cmd** to run all assertions. The distributed native DLL is ready to load.

## Build and run from the project root

Run these commands from the folder containing LibraryDesk.sln. No external NuGet package is required.

```text
dotnet build LibraryDesk.sln -c Release -m:1
dotnet run --no-build -c Release --project src/LibraryDesk -- demo
dotnet run --no-build -c Release --project tests/LibraryDesk.Tests
```

For an interactive session on Linux, run `cp -n data/sample.json data/library.json` once, then `dotnet run --project src/LibraryDesk`. On Windows Start.cmd performs that copy. Run a single command against any working file with `dotnet run --project src/LibraryDesk -- --data data/library.json books`. The demo always uses a temporary copy of data/sample.json.

## Demonstration

| Command in the console | Expected output or change |
| --- | --- |
| issue B03 Dana 2026-11-15 standard | Due 2026-11-29 |
| issue B03 Dana 2026-11-15 standard | Rejected. Book is already on loan. |
| renew B03 2026-11-20 | Due 2026-12-06 |
| return B03 2026-12-09 | Returned, fee 300 KZT |
| overdue 2026-11-15 | B01, Aida, 3 days late |
| popular | B01, B02 and B03 each have one issue. Ties use book ID. |

These steps start from sample.json. Reader IDs are single words. The standard term is 14 calendar days, the extended term is 28 days. A renewal must take place by the due date and adds 7 days to that date. A return on the due date is free. Every later day costs 100 KZT up to a total of 3,000 KZT. Reissue is allowed on the day of return. The CLI prints a rejection and exits with code 1 on a failed domain operation. In menu mode it accepts the next command.

## Source and tests

| File | Responsibility |
| --- | --- |
| src/LibraryDesk/Lending.cs | Book factory, immutable loans, active and returned states, three workflows, configurable loan periods |
| src/LibraryDesk/Outcomes.cs | Result.Map, Result.Bind, Maybe, FindMaybe and OrFail |
| src/LibraryDesk/Reports.cs | Lazy overdue selection, two materialised LINQ reports and stable tie-breakers |
| src/LibraryDesk/Ledger.cs | Validate and replay JSON history, dated snapshots, file locking and atomic replacement |
| src/LibraryDesk/NativeFees.cs and native/libraryfees.c | Managed adapter, exact native symbol, calling convention and fee calculation |
| src/LibraryDesk/Program.cs | Console, receipts and repeatable demo |
| tests/LibraryDesk.Tests/Program.cs | 23 deterministic named tests with explicit expected values and process exit status |

The runner is a console project. Use `dotnet run --project tests/LibraryDesk.Tests` to execute it. Test 17 launches the same runner in a new process with an invalid library path so a previously loaded DLL cannot mask the failure.

## Native build and resource handling

The included Linux x64 shared object was built with GCC 14.2.0. On Linux x64 run `sh native/build-linux.sh` to rebuild. The Windows x64 DLL was cross-compiled with Zig 0.13.0. The equivalent command from the project root is:

```text
zig cc -target x86_64-windows-gnu -std=c11 -O2 -shared -s native/libraryfees.c -o native/runtimes/win-x64/libraryfees.dll
```

Alternatively, use an x64 Native Tools Command Prompt with Visual Studio C++ build tools and run `native\build-windows.cmd`. Run the managed build after rebuilding the native binary. MSBuild copies the runtime folders to both project outputs.

P/Invoke calls library_fee with a cdecl 32-bit integer argument and result. The resolver selects the current platform and architecture under the application output folder. Set LIBRARYDESK_NATIVE to an absolute path to override the binary before starting a process. The loaded module stays available for the lifetime of the application. No pointer or native allocation crosses the call boundary. File streams, writer locks and child processes are disposed by their owners. JSON updates hold an exclusive lock, flush a temporary file next to the original, then replace it. A rejected workflow leaves the original bytes unchanged.

## Scope and limits

The catalogue treats each ID as one physical copy. Version 1 retains lending history to reconstruct past overdue reports and count issues. It replays under the fixed v1 lending and fine policies. Changing historical pricing would need policy versioning or stored historical receipts. The local file and in-memory replay are intended for a small catalogue. The sample contains fictional reader IDs. Sample data, code and tests are included.

## Repository and submitted version

- Branch: main
- Source commit in this exported copy: $Format:%H$
- Hosted repository URL: pending publication by the student.

The practice package includes a Git bundle of the source revision. Clone it locally from the extracted archive with `git clone -b main Submission/LibraryDesk.bundle LibraryDesk-checkout`. Open that checkout to continue development. In a Git checkout, use `git rev-parse HEAD` to obtain the exact identifier. Git expands the commit marker in README when creating an export with `git archive HEAD`.

For the assessed version, publish the authorised student version to an accessible repository, record its real URL in README and the project report, commit source changes, then export with `git archive --format=zip -o ../source.zip HEAD`. Enter the same commit from `git rev-parse HEAD` in the final report. Include the updated reports with that exported code in the submission ZIP. Grant the instructor access before the deadline if the repository is private.

## External components

LibraryDesk uses the .NET 8 standard libraries and the C toolchains listed above.
