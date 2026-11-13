namespace FastLabel.Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
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

            var role = user.IsAdmin ? "Admin" : "Standard";

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
                user = new { user.Id, user.Email, user.Name, user.IsAdmin } 
            });
        });

        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { message = "auth.success.logout" });
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal userPrincipal, AppDbContext db) =>
        {
            if (userPrincipal.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var idClaim = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                Id = user.Id,
                Name = user.Name ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsAdmin = user.IsAdmin
            });
        }).RequireAuthorization();

        group.MapPost("/forgot-password", async (ForgotPasswordDto dto, HttpContext httpContext, AppDbContext db, IEmailService emailService, IConfiguration configuration) =>
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

            var languageCode = httpContext.GetRequestLanguage();

            var emailTexts = await db.Texts
                .Where(t => t.Tag == "email.reset.subject" || t.Tag == "email.reset.body")
                .Select(t => new
                {
                    t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Code == languageCode)
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

        group.MapPost("/reset-password", async (ResetPasswordDto dto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
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

        group.MapPost("/register", async (RegisterRequest dto, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.email_already_taken" });
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow,
                EmailVerificationToken = Guid.NewGuid().ToString()
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

            var languageCode = httpContext.GetRequestLanguage();

            var emailTexts = await db.Texts
                .Where(t => t.Tag == "email.activation.subject" || t.Tag == "email.activation.body")
                .Select(t => new
                {
                    t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Code == languageCode)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            var subject = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.subject")?.Value
                        ?? "Activate your FastLabel account";

            var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                                    ?? "Hello {0},\n\nPlease click the following link to activate your account:\n{1}";

            var message = string.Format(bodyTemplate, user.Name, verificationLink);

            await emailService.SendEmailAsync(user.Email, subject, message);

            return Results.Ok(new { message = "auth.success.registered_check_email" });
        });

        group.MapGet("/verify-email", async (string token, AppDbContext db, IConfiguration configuration) =>
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