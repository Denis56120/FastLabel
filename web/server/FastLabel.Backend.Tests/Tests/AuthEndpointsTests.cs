namespace FastLabel.Backend.Tests.Tests;

public class AuthEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{  
    private readonly CustomWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task VerifyEmail_WithValidToken_ActivatesUserAndClearsToken()
    {
        // Arrange - 1. Créer un utilisateur inactif avec un token de vérification en base
        string verificationToken = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var inactiveStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.inactive")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            var user = new User
            {
                Name = "Jean Test",
                Email = "jean.test@exemple.fr",
                PasswordHash = "hashed_password",
                EmailVerificationToken = verificationToken,
                BasicStatusId = inactiveStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // Act - 2. Appeler l'endpoint de vérification d'e-mail (AllowAutoRedirect = false pour intercepter la redirection HTTP)
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/api/auth/verify-email?token={verificationToken}");

        // Assert - 3. Vérifier la redirection HTTP (vers le client avec ?activated=true)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("activated=true", response.Headers.Location?.Query);

        // Assert - 4. Vérifier en base de données que le user est actif et que le token a été nettoyé
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var updatedUser = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(bs => bs.Name)
                .FirstOrDefaultAsync(u => u.Email == "jean.test@exemple.fr");

            Assert.NotNull(updatedUser);
            Assert.Null(updatedUser.EmailVerificationToken);
            Assert.Equal("status.active", updatedUser.BasicStatus?.Name?.Tag);
        }
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

    [Fact]
    public async Task ForgotPassword_FullFlow_AllowsResetAndLoginWithNewPassword()
    {
        // Arrange - 1. Créer un utilisateur ACTIF en base
        var email = "reset.user@exemple.fr";
        var newPassword = "BrandNewPassword123!";
        string resetToken;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // On s'assure de récupérer explicitement le statut actif pour que le login final passe
            var activeStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.active")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            db.Users.Add(new User
            {
                Name = "Marc Oubli",
                Email = email,
                PasswordHash = "old_hashed_password",
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            });
            await db.SaveChangesAsync();
        }

        _factory.EmailServiceMock.Invocations.Clear();

        // Act 1 - Demande de réinitialisation (/forgot-password)
        var forgotDto = new ForgotPasswordDto { Email = email };
        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotDto);

        // Assert 1 - Réponse HTTP 200 et récupération du token en base
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            Assert.NotNull(userInDb);
            Assert.False(string.IsNullOrWhiteSpace(userInDb.PasswordResetToken));
            Assert.NotNull(userInDb.ResetTokenExpired);
            Assert.True(userInDb.ResetTokenExpired > DateTime.UtcNow);

            resetToken = userInDb.PasswordResetToken!;

            _factory.EmailServiceMock.Verify(
                e => e.SendEmailAsync(
                    email,
                    It.IsAny<string>(),
                    It.Is<string>(body => body.Contains(resetToken))),
                Times.Once);
        }

        // Act 2 - Réinitialisation du mot de passe (/reset-password)
        var resetDto = new ResetPasswordDto
        {
            Email = email,
            Token = resetToken,
            NewPassword = newPassword
        };
        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

        // Assert 2 - Réponse HTTP 200 et nettoyage du token en base
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            Assert.NotNull(updatedUser);
            Assert.Null(updatedUser.PasswordResetToken);
            Assert.Null(updatedUser.ResetTokenExpired);
        }

        // Act 3 - Vérification de l'authentification avec le nouveau mot de passe (/login)
        var loginDto = new LoginDto
        {
            Email = email,
            Password = newPassword
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert 3 - Connexion réussie et cookie d'authentification posé
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ReturnsOkWithoutSendingEmail()
    {
        // Arrange
        _factory.EmailServiceMock.Invocations.Clear();
        var dto = new ForgotPasswordDto { Email = "inconnu.total@exemple.fr" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", dto);

        // Assert - 1. Doit toujours renvoyer 200 OK (sécurité contre l'énumération d'emails)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert - 2. Aucun e-mail ne doit être envoyé
        _factory.EmailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var resetDto = new ResetPasswordDto
        {
            Email = "inexistant@exemple.fr",
            Token = "token-inexistant-ou-invalide-12345",
            NewPassword = "NewSecurePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("auth.error.invalid_or_expired_token", errorResponse?.Message);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ReturnsBadRequest()
    {
        // Arrange - 1. Créer un utilisateur avec un token dont la date d'expiration est dépassée
        var email = "expired.token@exemple.fr";
        string expiredToken = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.active")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            db.Users.Add(new User
            {
                Name = "Périmé User",
                Email = email,
                PasswordHash = "hashed_password",
                PasswordResetToken = expiredToken,
                ResetTokenExpired = DateTime.UtcNow.AddMinutes(-5), // Expiré il y a 5 minutes
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            });
            await db.SaveChangesAsync();
        }

        var resetDto = new ResetPasswordDto
        {
            Email = email,
            Token = expiredToken,
            NewPassword = "NewSecurePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", resetDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("auth.error.invalid_or_expired_token", errorResponse?.Message);

        // Vérifier que le mot de passe n'a pas été modifié et que le token est toujours en base (ou géré selon ta logique)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            Assert.NotNull(userInDb);
            Assert.Equal(expiredToken, userInDb.PasswordResetToken); // Le token reste tant qu'il n'est pas consommé valablement
        }
    }

    [Fact]
    public async Task EndToEnd_PasswordReset_Flow()
    {
        // Arrange - 1. Créer un utilisateur actif avec un ancien mot de passe connu
        var email = "e2e.reset@exemple.fr";
        var oldPassword = "OldPassword123!";
        var newPassword = "BrandNewSecurePassword456!";

        using (var scope = _factory.Services.CreateScope())
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
                Name = "E2E Reset User",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, oldPassword),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        _factory.EmailServiceMock.Invocations.Clear();

        // Act - 2. Étape 1 : Demande de réinitialisation (/forgot-password)
        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordDto { Email = email });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        // Récupérer le token généré en base de données
        string resetToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userInDb = await db.Users.FirstAsync(u => u.Email == email);

            Assert.NotNull(userInDb.PasswordResetToken);
            resetToken = userInDb.PasswordResetToken;
        }

        // Act - 3. Étape 2 : Changement effectif du mot de passe (/reset-password)
        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordDto
        {
            Email = email,
            Token = resetToken,
            NewPassword = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // Act - 4. Étape 3a : Essayer de se connecter avec l'ANCIEN mot de passe (doit échouer)
        var loginWithOldResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = oldPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, loginWithOldResponse.StatusCode);

        // Act - 5. Étape 3b : Se connecter avec le NOUVEAU mot de passe (doit réussir)
        var loginWithNewResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = newPassword
        });

        // Assert - Vérification finale de la session active
        Assert.Equal(HttpStatusCode.OK, loginWithNewResponse.StatusCode);
        Assert.True(loginWithNewResponse.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndSetsAuthCookie()
    {
        // Arrange
        var email = "login.success@exemple.fr";
        var password = "ValidPassword123!";

        using (var scope = _factory.Services.CreateScope())
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
                Name = "Login Success",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var loginDto = new LoginDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsBadRequest()
    {
        // Arrange
        var email = "login.badpass@exemple.fr";
        var password = "ValidPassword123!";

        using (var scope = _factory.Services.CreateScope())
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
                Name = "Bad Pass",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var loginDto = new LoginDto
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("auth.error.invalid_credentials", errorResponse?.Message);
    }

    [Fact]
    public async Task Login_WithInactiveAccount_ReturnsBadRequest()
    {
        // Arrange
        var email = "login.inactive@exemple.fr";
        var password = "ValidPassword123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inactiveStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.inactive")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            var user = new User
            {
                Name = "Inactive User",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = inactiveStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var loginDto = new LoginDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("auth.error.account_inactive", errorResponse?.Message);
    }

    [Fact]
    public async Task Logout_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Act - Tentative de logout sans être connecté
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert - Doit être rejeté par [RequireAuthorization]
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_ReturnsOkAndClearsSession()
    {
        // Arrange - 1. Créer et connecter un utilisateur valide
        var email = "logout.user@exemple.fr";
        var password = "ValidPassword123!";

        using (var scope = _factory.Services.CreateScope())
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
                Name = "Logout User",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // Effectuer le login pour récupérer le cookie de session sur le client HTTP de test
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Récupérer explicitement le cookie de la réponse du login pour l'injecter dans la requête de logout
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookieHeader = cookies.First();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", cookieHeader.Split(';')[0]);

        // Act - 2. Appeler le logout avec la session active
        var logoutResponse = await _client.SendAsync(request);

        // Assert - 3. Vérifier que la déconnexion réussit
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var result = await logoutResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.success.logout", result.GetProperty("message").GetString());

        // Act - 4. Vérifier qu'une requête ultérieure sans cookie renvoie bien Unauthorized
        var subsequentResponse = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, subsequentResponse.StatusCode);
    }
}