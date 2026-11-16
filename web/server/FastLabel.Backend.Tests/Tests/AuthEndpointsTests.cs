namespace FastLabel.Backend.Tests.Tests;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_CreatesInactiveUserAndSendsActivationEmail()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Name = "Jean Dupont",
            Email = "jean.dupont@exemple.fr",
            Password = "SuperPassword123!"
        };

        // Réinitialiser les appels précédents sur le mock d'email
        _factory.EmailServiceMock.Invocations.Clear();

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerDto);

        // Assert - 1. Réponse HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert - 2. Vérification en base de données
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userInDb = await db.Users
            .Include(u => u.BasicStatus)
                .ThenInclude(bs => bs.Name)
            .FirstOrDefaultAsync(u => u.Email == registerDto.Email);

        Assert.NotNull(userInDb);
        Assert.Equal("Jean Dupont", userInDb.Name);
        Assert.False(userInDb.IsAdmin);
        Assert.False(userInDb.IsReseller);
        Assert.Null(userInDb.ResellerId);
        Assert.NotEqual(registerDto.Password, userInDb.PasswordHash); // Mot de passe bien haché
        Assert.False(string.IsNullOrWhiteSpace(userInDb.EmailVerificationToken));
        Assert.Equal("status.inactive", userInDb.BasicStatus?.Name?.Tag);

        // Assert - 3. Vérification de l'appel au service d'envoi d'e-mail
        _factory.EmailServiceMock.Verify(
            e => e.SendEmailAsync(
                registerDto.Email,
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains(userInDb.EmailVerificationToken!))),
            Times.Once);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsBadRequest()
    {
        // Arrange : on crée d'abord un premier utilisateur
        var email = "doublon@exemple.fr";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Name = "Premier Utilisateur",
            Email = email,
            Password = "Password123!"
        });

        // Act : tentative de réinscription avec le même email
        var duplicateResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Name = "Deuxième Utilisateur",
            Email = email,
            Password = "AutrePassword123!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
        var error = await duplicateResponse.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.NotNull(error);
        Assert.Equal("auth.error.email_already_taken", error.Message);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidDto = new RegisterDto
        {
            Name = "Test Invalide",
            Email = "pas-un-email-valide",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", invalidDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}