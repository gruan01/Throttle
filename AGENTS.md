# AsNum.Throttle — AI Agent Guide

## Project Overview

A .NET library for rate-limiting action execution within configurable time windows. Supports in-process and cross-process (Redis) throttling with pluggable strategy components.

**NuGet:** [`AsNum.Throttle`](https://www.nuget.org/packages/AsNum.Throttle/) | [`AsNum.Throttle.Redis`](https://www.nuget.org/packages/AsNum.Throttle.Redis/)

## Build & Test

```bash
# Build solution (requires .NET 8/9/10 SDK)
dotnet build .\AsNum.Throttle.sln

# Run unit tests (xUnit, targetting net9.0)
dotnet test .\AsNum.Throttle.UnitTest\AsNum.Throttle.UnitTest.csproj
```

## Project Structure

| Project | Description |
|---|---|
| `AsNum.Throttle` | Core library — multi-targets `net8;net9;net10.0` |
| `AsNum.Throttle.Redis` | Redis-backed cross-process counter & config updater |
| `AsNum.Throttle.UnitTest` | xUnit tests (net9.0) |
| `AsNum.Throttle.TestConsole.N5` | Console test harness (net9) |

Legacy (obsolete, not in active build): `CrossProcess`, `Performance`, `Statistic`, `TestConsole` (.NET Framework), `TestConsole462`.

## Architecture — Strategy Pattern with 3 Pluggable Components

`Throttle` composes three abstractions that can be swapped independently:

```
Throttle
├── BaseBlock (concurrency/semaphore — DefaultBlock uses Channel<byte>)
├── BaseCounter (rate tracking — DefaultCounter uses Timer)
└── BaseCfgUpdater (runtime config — DefaultCfgUpdater is no-op)
```

- **`Throttle.Execute(Action/Func)`** — Queues work; 8 overloads (sync/async, with/without state).
- **`Throttle.Select()`** — Lightweight rate-gate check without queuing.
- **`Throttle.Dispose()`** — Disposes only components implementing `IAutoDispose` (externally-injected components are not auto-disposed).

See [README.md](./README.md) for basic usage examples.

## Key Conventions & Pitfalls

### Coding Conventions
- **Nullable:** Enabled (`<Nullable>enable</Nullable>`)
- **LangVersion:** C# 14 (`LangVersion` 14) — core & Redis projects
- **Namespaces:** File-scoped (`namespace AsNum.Throttle;`)
- **IDisposable:** Standard `Dispose(bool)` + finalizer + `GC.SuppressFinalize` pattern throughout
- **IDisposableAnalyzers:** Enforced at error level in `.editorconfig` (IDISP001–IDISP025)
- **XML docs:** Public API has Chinese-language comments; CS1591 suppressed
- **Naming:** Interfaces prefixed with `I`; PascalCase for types, methods, properties, events

### Known Quirks (Don't "Fix" These)
- **`DefaultCfgUpater`** — Class name intentionally misspelled (missing 'd' — `Upater` not `Updater`)
- **`DefaultleLogger`** — Class name has double 'e' (not `DefaultLogger`)
- **`WrapFuncTask.cs`** and **`IUnwrap.cs`** — Entirely commented out (legacy dead code, preserved for reference)
- **`CrossProcessBlock`**, **`RedisPerformanceCounter`**, **`ThrottlePerformanceCounter`** — Marked `[Obsolete]`, not in active build

### Important Design Details
- All three `Throttle` constructor-injected components are **public properties** — they can be swapped after construction
- `BaseBlock.Acquire()`/`Release()` form a counting semaphore via `Channel<byte>` — not thread-blocking, uses async wait
- `BaseCounter` uses `Interlocked.Add`/`Exchange` for thread safety; `System.Threading.Timer` resets count each period
- `RedisCounter` uses Lua scripts (`INCRBY` + `EXPIRE`) for atomic cross-process rate counting
- The `OnPeriodElapsed` event fires when the counter period resets
- `tskCount` (volatile int) is used alongside `ConcurrentQueue<Task>` to avoid expensive `IsEmpty` checks in hot loops
