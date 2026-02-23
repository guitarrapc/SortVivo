# SortAlgorithmLab - Project Instructions

## What is this project?

This is a C# sorting algorithm laboratory for educational and performance analysis purposes. It implements various sorting algorithms with comprehensive statistics tracking and visualization support.

**Tech Stack:** C# (.NET 10+), xUnit, BenchmarkDotNet

## Project Structure

- `src/SortAlgorithm/` - Core sorting algorithms and interfaces
  - `Algorithms/` - Sorting algorithm implementations
  - `Contexts/` - Statistics and visualization contexts
- `tests/SortAlgorithm.Tests/` - Unit tests for all algorithms
- `sandbox/` - Benchmark and experimental code
- `.github/agent_docs/` - Detailed implementation guidelines

## Important Guidelines

When implementing or reviewing sorting algorithms, refer to these detailed guides:

- **[Architecture](agent_docs/architecture.md)** - Understand the Context + SortSpan pattern
- **[Coding Style](agent_docs/coding_style.md)** - C# style conventions for this project
- **[Implementation Template](agent_docs/implementation_template.md)** - Template for new algorithms
- **[Performance Requirements](agent_docs/performance_requirements.md)** - Zero-allocation, aggressive inlining, and memory management
- **[Sandbox Code Guidelines](agent_docs/sandbox_code_guidelines.md)** - How to create and run sandbox code for testing ideas
- **[SortSpan Usage](agent_docs/sortspan_usage.md)** - How to use SortSpan for all operations
- **[Testing Guidelines](agent_docs/testing_guidelines.md)** - Writing/Run effective unit tests

**Key Rule:** Always use `SortSpan<T, TComparer, TContext>` methods (`Read`, `Write`, `Compare`, `Swap`, `CopyTo`) instead of direct array access. This ensures accurate statistics tracking. All algorithms use the generic `TComparer : IComparer<T>` pattern for zero-allocation devirtualized comparisons, with convenience overloads that delegate via `new ComparableComparer<T>()`. This follows the same pattern as `MemoryExtensions.Sort` in dotnet/runtime - runtime validation instead of compile-time constraints.

**Script Rule:** Don't write any multi-line PowerShell Code in the shell. If you need to run a script, create a file then executte it.

## How to Work on This Project

### Running Tests

See [Testing Guidelines](agent_docs/testing_guidelines.md) for details on writing and running tests. To run all tests:

```shell
dotnet test
```

To run specific sort's tests (e.g., PowerSortTests):

```shell
dotnet run --treenode-filter /*/*/PowerSortTests/*
```

### Running Benchmarks

```shell
cd src/SortAlgorithm.Benchmark
dotnet run -c Release
```

### Building the Project

```shell
dotnet build
```

### Run Some Script

You can create a .cs file in `sandbox/DotnetFiles/` and run it directly. See [Sandbox Code Guidelines](agent_docs/sandbox_code_guidelines.md) for details and script sample.

use `sandbox/DotnetFiles/Sample.cs` for template:

```shell
dotnet run sandbox/DotnetFiles/YourCsFile.cs
```

## Progressive Disclosure

Before implementing a new sorting algorithm or making significant changes:

1. Read the relevant documentation files in `.github/agent_docs/`
2. Review existing similar implementations in `src/SortAlgorithm/Algorithms/`
3. Check corresponding tests in `tests/SortAlgorithm.Tests/`

Ask which documentation files you need if you're unsure what to read.
