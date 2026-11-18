namespace FastLabel.Backend.Tests.Factory;

[CollectionDefinition("FastLabel Test Collection")]
public class DatabaseCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // Cette classe ne contient rien. Elle sert uniquement à regrouper 
    // les tests autour d'une instance unique de CustomWebApplicationFactory.
}