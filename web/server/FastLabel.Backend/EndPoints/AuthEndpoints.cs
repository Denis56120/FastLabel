namespace FastLabel.Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var authGroup = routes.MapGroup("/api/auth");

        authGroup.MapPost("/login", async (LoginDto request, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationHelpers.TryValidateModel(request, out var errorResult))
            {
                return errorResult!;
            }

            var user = await DbHelpers.GetUserByEmailAsync(db, request.Email);            
            if (user == null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_credentials" });
            }

            var statusTag = await DbHelpers.GetStatusTagByIdAsync(db, user.BasicStatusId);
            if (statusTag == "status.inactive")
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.account_inactive" });
            }

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

        authGroup.MapPost("/forgot-password", async (ForgotPasswordDto dto, HttpContext httpContext, AppDbContext db, IEmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await DbHelpers.GetUserByEmailAsync(db, dto.Email); 
            if (user == null)
            {
                return Results.Ok(new { message = "auth.success.forgot_password" });
            }

            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpired = DateTime.UtcNow.AddHours(2);
            await db.SaveChangesAsync();

            var clientUrl = ConfigurationHelpers.GetClientUrl(configuration);
            var resetLink = $"{clientUrl}/reset-password?token={user.PasswordResetToken}";

            var (languageId, language) = await DbHelpers.GetLanguageAsync(httpContext, db);

            var emailTexts = await DbHelpers.GetLocalizedTextsAsync(db, language, "email.reset.subject", "email.reset.body");
            
            var subject = emailTexts.GetValueOrDefault("email.reset.subject", string.Empty);
            var bodyTemplate = emailTexts.GetValueOrDefault("email.reset.body", string.Empty);

            var message = string.Format(bodyTemplate, user.Name, resetLink);

            await emailService.SendEmailAsync(user.Email, subject, message);

            return Results.Ok(new { message = "auth.success.forgot_password" });
        });

        authGroup.MapPost("/reset-password", async (ResetPasswordDto dto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await DbHelpers.GetUserByPasswordResetTokenAsync(db, dto.Token);
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

        authGroup.MapPost("/register", async (RegisterDto dto, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.email_already_taken" });
            }

            var (languageId, language) = await DbHelpers.GetLanguageAsync(httpContext, db);
            int inactiveStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.inactive");

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow,
                EmailVerificationToken = Guid.NewGuid().ToString(),
                LanguageId = languageId,
                BasicStatusId = inactiveStatusId
            };
            user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

            await DbHelpers.CreateUserAndSendActivationEmailAsync(db, emailService, configuration, user, language);

            return Results.Ok(new { message = "auth.success.registered_check_email" });
        });

        authGroup.MapGet("/verify-email", async (string token, AppDbContext db, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_token" });
            }

            var user = await DbHelpers.GetUserByEmailVerificationTokenAsync(db, token);
            if (user == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.invalid_or_expired_token" });
            }

            user.BasicStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");
            user.EmailVerificationToken = null;
            await db.SaveChangesAsync();

            var clientUrl = ConfigurationHelpers.GetClientUrl(configuration);
            return Results.Redirect($"{clientUrl}/?activated=true");
        }).DisableAntiforgery();
    }
}