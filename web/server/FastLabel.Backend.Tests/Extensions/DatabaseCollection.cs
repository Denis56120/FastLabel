namespace FastLabel.Backend.Tests.Extensions;

[CollectionDefinition("FastLabel Test Collection")]
public class DatabaseCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // Cette classe ne contient rien. Elle sert uniquement à regrouper 
    // les tests autour d'une instance unique de CustomWebApplicationFactory.
}