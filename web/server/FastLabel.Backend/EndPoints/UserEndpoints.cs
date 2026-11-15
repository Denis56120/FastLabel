namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        // ==========================================
        // 1. AUTHENTIFICATION ENDPOINTS (/api/auth)
        // ==========================================
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

            var role = user.IsAdmin ? "Admin" : "User";

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
                user = new { user.Id, user.Email, user.Name, user.IsAdmin, user.LanguageId } 
            });
        });

        authGroup.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { message = "auth.success.logout" });
        }).RequireAuthorization();

        authGroup.MapPost("/forgot-password", async (ForgotPasswordDto dto, HttpContext httpContext, AppDbContext db, IEmailService emailService, IConfiguration configuration) =>
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

        authGroup.MapPost("/register", async (RegisterDto dto, HttpContext httpContext, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            if (await db.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "auth.error.email_already_taken" });
            }

            var frenchLanguage = await db.Languages.FirstOrDefaultAsync(l => l.Code == "fr");
            int defaultLanguageId = frenchLanguage?.Id ?? await db.Languages.Select(l => l.Id).FirstOrDefaultAsync();

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


        // ==========================================
        // 2. USER MANAGEMENT ENDPOINTS (/api/users)
        // ==========================================
        routes.MapGet("/api/users/me", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var userDto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == userId)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (userDto == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            return Results.Ok(userDto);
        }).RequireAuthorization();

        routes.MapPut("/api/users/me", async (UserDto updatedDto, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            bool languageExists = await db.Languages.AnyAsync(l => l.Id == updatedDto.LanguageId);
            if (!languageExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.language_not_found" });
            }

            bool emailChanged = !string.Equals(user.Email, updatedDto.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                bool emailExists = await db.Users.AnyAsync(u => u.Email == updatedDto.Email && u.Id != userId);
                if (emailExists)
                {
                    return Results.BadRequest(new ErrorResponseDto { Message = "user.error.email_already_taken" });
                }

                user.Email = updatedDto.Email;
                user.EmailVerificationToken = Guid.NewGuid().ToString();

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
                            ?? string.Empty;

                var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                                            ?? string.Empty;

                var message = string.Format(bodyTemplate, updatedDto.Name, verificationLink);

                await emailService.SendEmailAsync(user.Email, subject, message);
            }

            user.Name = updatedDto.Name;
            user.LanguageId = updatedDto.LanguageId;

            if (!string.IsNullOrWhiteSpace(updatedDto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, updatedDto.Password);
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { emailChanged });
        }).RequireAuthorization();

        var adminGroup = routes.MapGroup("/api/users").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async (AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var users = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(users);
        });

        adminGroup.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var dto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == id)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (dto == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            return Results.Ok(dto);
        });

        adminGroup.MapPost("/", async (UserDto dto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            bool languageExists = await db.Languages.AnyAsync(l => l.Id == dto.LanguageId);
            if (!languageExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.language_not_found" });
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                IsAdmin = dto.IsAdmin,
                LanguageId = dto.LanguageId,
                BasicStatusId = 1,
                CreatedAt = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
            }

            db.Users.Add(user);
            await db.SaveChangesAsync();

            dto.Id = user.Id;
            dto.Password = null; 
            return Results.Created($"/api/users/{user.Id}", dto);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UserDto updatedDto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            bool languageExists = await db.Languages.AnyAsync(l => l.Id == updatedDto.LanguageId);
            if (!languageExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.language_not_found" });
            }

            user.Name = updatedDto.Name;
            user.Email = updatedDto.Email;
            user.IsAdmin = updatedDto.IsAdmin;
            user.LanguageId = updatedDto.LanguageId;

            if (!string.IsNullOrWhiteSpace(updatedDto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, updatedDto.Password);
            }

            await db.SaveChangesAsync();
            return Results.Ok(user);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}