namespace FastLabel.Backend.Tests.Factory;

public static class TestWebApplicationFactoryExtensions
{
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this TestWebApplicationFactory factory, string email, string password = "ValidPassword123!", bool isAdmin = false, bool isReseller = false, int languageId = 1)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.active")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            var user = new User
            {
                Name = $"User {email.Split('@')[0]}",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = languageId,
                IsReseller = isReseller,
                IsAdmin = isAdmin
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateUnauthenticatedClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });

        if (!loginResponse.IsSuccessStatusCode)
        {
            var errorContent = await loginResponse.Content.ReadAsStringAsync();
            throw new Exception($"Échec de l'authentification pour {email} (Status: {loginResponse.StatusCode}) : {errorContent}");
        }

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        client.DefaultRequestHeaders.Add("Cookie", cookies.First().Split(';')[0]);

        return client;
    }

    public static HttpClient CreateUnauthenticatedClient(this TestWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }
}