namespace FastLabel.Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var authGroup = routes.MapGroup("/api/auth");

        authGroup.MapPost("/login", async (LoginDto request, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(request, out var errorResult))
            {
                return errorResult!;
            }

            var user = await db.Users
                .OrderBy(u => u.Id) 
                .FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_credentials" });
            }

            var statusTag = await db.Texts
                .Join(db.BasicStatuses, t => t.Id, bs => bs.NameTextId, (t, bs) => new { bs.Id, t.Tag })
                .Where(x => x.Id == user.BasicStatusId)
                .OrderBy(x => x.Id)
                .Select(x => x.Tag)
                .FirstOrDefaultAsync();

            if (statusTag == "status.inactive")
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.account_inactive" });
            }

            // Détermination du rôle selon les flags de l'entité User
            var role = user.IsAdmin ? "Admin" : (user.IsReseller ? "Reseller" : "User");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Role, role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });

            return Results.Ok(new
            {
                user = new { user.Id, user.Email, user.Name, user.IsAdmin, user.IsReseller, user.LanguageId } 
            });
        });

        authGroup.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { message = "auth.success.logout" });
        }).RequireAuthorization();

        authGroup.MapPost("/forgot-password", async (ForgotPasswordDto dto, HttpContext httpContext, AppDbContext db, EmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await db.Users
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
            {
                return Results.Ok(new { message = "auth.success.forgot_password" });
            }

            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpired = DateTime.UtcNow.AddHours(2);
            await db.SaveChangesAsync();

            var clientUrl = configuration["Url:ClientUrl"] ?? "https://fastlabel.com";
            var resetLink = $"{clientUrl}/reset-password?token={user.PasswordResetToken}";

            // Récupération de la langue via le cookie du navigateur (ou 'en' par défaut)
            string language = HttpContextExtensions.DefaultLanguage;
            if (httpContext.Request.Cookies.TryGetValue("language", out var cookieLang) && !string.IsNullOrEmpty(cookieLang))
            {
                language = cookieLang;
            }

            var emailTexts = await db.Texts
                .Where(t => t.Tag == "email.reset.subject" || t.Tag == "email.reset.body")
                .Select(t => new
                {
                    t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            var subject = emailTexts.FirstOrDefault(t => t.Tag == "email.reset.subject")?.Value
                        ?? "Reset your FastLabel password";

            var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.reset.body")?.Value
                                    ?? "Hello {0},\n\nPlease click the following link to reset your password:\n{1}";

            var message = string.Format(bodyTemplate, user.Name, resetLink);

            await emailService.SendEmailAsync(user.Email, subject, message);

            return Results.Ok(new { message = "auth.success.forgot_password" });
        });

        authGroup.MapPost("/reset-password", async (ResetPasswordDto dto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await db.Users
                .OrderBy(u => u.Id) 
                .FirstOrDefaultAsync(u => u.PasswordResetToken == dto.Token);

            if (user == null || user.ResetTokenExpired < DateTime.UtcNow)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_or_expired_token" });
            }

            user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
            user.PasswordResetToken = null;
            user.ResetTokenExpired = null;

            await db.SaveChangesAsync();

            return Results.Ok(new { message = "auth.success.password_reset" });
        });

        authGroup.MapPost("/register", async (RegisterDto dto, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher, EmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.email_already_taken" });
            }

            var englishLanguage = await db.Languages.FirstOrDefaultAsync(l => l.Code == HttpContextExtensions.DefaultLanguage);
            int defaultLanguageId = englishLanguage?.Id ?? await db.Languages.Select(l => l.Id).FirstOrDefaultAsync();

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow,
                EmailVerificationToken = Guid.NewGuid().ToString(),
                LanguageId = defaultLanguageId
            };

            user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

            var inactiveStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.inactive")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstOrDefaultAsync();

            if (inactiveStatus != null)
            {
                user.BasicStatusId = inactiveStatus.Id;
            }
            else
            {
                var fallbackStatus = await db.BasicStatuses.OrderBy(s => s.Id).Skip(1).FirstOrDefaultAsync();
                if (fallbackStatus != null)
                {
                    user.BasicStatusId = fallbackStatus.Id;
                }
            }

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var serverUrl = configuration["Url:ServerUrl"] ?? "https://fastlabel.com";
            var verificationLink = $"{serverUrl}/api/auth/verify-email?token={user.EmailVerificationToken}";

            // Récupération de la langue via le cookie du navigateur (ou 'en' par défaut)
            string language = HttpContextExtensions.DefaultLanguage;
            if (httpContext.Request.Cookies.TryGetValue("language", out var cookieLang) && !string.IsNullOrEmpty(cookieLang))
            {
                language = cookieLang;
            }

            var emailTexts = await db.Texts
                .Where(t => t.Tag == "email.activation.subject" || t.Tag == "email.activation.body")
                .Select(t => new
                {
                    t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            var subject = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.subject")?.Value
                        ?? string.Empty;

            var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                                    ?? string.Empty;

            var message = string.Format(bodyTemplate, user.Name, verificationLink);

            await emailService.SendEmailAsync(user.Email, subject, message);

            return Results.Ok(new { message = "auth.success.registered_check_email" });
        });

        authGroup.MapGet("/verify-email", async (string token, AppDbContext db, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_token" });
            }

            var user = await db.Users
                .OrderBy(u => u.Id) 
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
            if (user == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_or_expired_token" });
            }

            var activeStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.active")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstOrDefaultAsync();

            if (activeStatus != null)
            {
                user.BasicStatusId = activeStatus.Id;
            }

            user.EmailVerificationToken = null;
            await db.SaveChangesAsync();

            var clientUrl = configuration["Url:ClientUrl"] ?? "https://fastlabel.com";
            return Results.Redirect($"{clientUrl}/?activated=true");
        });
    }
}