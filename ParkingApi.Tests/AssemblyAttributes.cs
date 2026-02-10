using Xunit;

// Enable parallel execution with controlled concurrency
// Unit tests use in-memory DB (parallel safe)
// Integration tests use namespace isolation (parallel safe)
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
