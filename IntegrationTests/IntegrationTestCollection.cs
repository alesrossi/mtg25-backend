namespace IntegrationTests;

/// <summary>
/// Test collection to control parallel execution of integration tests.
/// By using the same collection name, xUnit will run these test classes sequentially
/// instead of in parallel, preventing database conflicts.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}