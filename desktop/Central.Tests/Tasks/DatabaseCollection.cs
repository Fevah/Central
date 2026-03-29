namespace Central.Tests.Tasks;

/// <summary>
/// xUnit collection definition that prevents parallel execution of DB integration tests.
/// All test classes with [Collection("Database")] run sequentially.
/// </summary>
[CollectionDefinition("Database", DisableParallelization = true)]
public class DatabaseCollection;
