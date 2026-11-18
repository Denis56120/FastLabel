namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users/me", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var userInfo = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    User = u,
                    LangCode = u.Language != null ? u.Language.Code : HttpContextHelpers.DefaultLanguage
                })
                .FirstOrDefaultAsync();

            if (userInfo == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            var user = userInfo.User;
            var userLangCode = userInfo.LangCode;

            httpContext.SetLanguageCookie(userLangCode);

            var userDto = await db.Users
                .Include(u => u.Reseller)
                .Where(u => u.Id == userId)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    IsReseller = u.IsReseller,
                    ResellerId = u.ResellerId,
                    ResellerName = u.Reseller != null ? u.Reseller.Name : null,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == userLangCode)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                        ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            return Results.Ok(userDto);
        }).RequireAuthorization();

        routes.MapPut("/api/users/me", async (UserDto updatedDto, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationHelpers.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var user = await DbHelpers.GetUserByIdAsync(db, userId);  
            if (user == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            var language = await DbHelpers.GetLanguageByIdAsync(db, updatedDto.LanguageId); 
            if (language == null)
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

                var serverUrl = ConfigurationHelpers.GetServerUrl(configuration);
                var verificationLink = $"{serverUrl}/api/auth/verify-email?token={user.EmailVerificationToken}";

                var emailTexts = await DbHelpers.GetLocalizedTextsAsync(db, language.Code, "email.activation.subject", "email.activation.body");

                var subject = emailTexts.GetValueOrDefault("email.activation.subject", string.Empty);
                var bodyTemplate = emailTexts.GetValueOrDefault("email.activation.body", string.Empty);

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

            httpContext.SetLanguageCookie(language.Code);

            return Results.Ok(new { emailChanged });
        }).RequireAuthorization();

        var adminGroup = routes.MapGroup("/api/users").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async (
            int page, 
            int pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByName,
            string? filterByEmail,
            string? filterByReseller,
            AppDbContext db, 
            ClaimsPrincipal claimsUser, 
            HttpContext httpContext) =>
        {
            var (adminId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (adminId == 0) return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500;

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, adminId);

            var query = db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(u => u.Reseller)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(u => u.Name != null && u.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByEmail))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(filterByEmail));
            }

            if (!string.IsNullOrWhiteSpace(filterByReseller))
            {
                query = query.Where(u => u.Reseller != null && u.Reseller.Name != null && u.Reseller.Name.Contains(filterByReseller));
            }

            var naturalComparer = new NaturalStringHelpers();

            var result = await query.ToPagedResultAsync(
                page,
                pageSize,
                sortBy,
                sortAscending,
                u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    IsReseller = u.IsReseller,
                    ResellerId = u.ResellerId,
                    ResellerName = u.Reseller != null ? u.Reseller.Name : null,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                        ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "name" => ascending ? items.OrderBy(x => x.Name, naturalComparer) : items.OrderByDescending(x => x.Name, naturalComparer),
                    "email" => ascending ? items.OrderBy(x => x.Email, naturalComparer) : items.OrderByDescending(x => x.Email, naturalComparer),
                    "role" => ascending ? items.OrderBy(x => x.IsAdmin).ThenBy(x => x.IsReseller) : items.OrderByDescending(x => x.IsAdmin).ThenByDescending(x => x.IsReseller),
                    "resellername" => ascending ? items.OrderBy(x => x.ResellerName, naturalComparer) : items.OrderByDescending(x => x.ResellerName, naturalComparer),
                    "basicstatusname" => ascending ? items.OrderBy(x => x.BasicStatusName, naturalComparer) : items.OrderByDescending(x => x.BasicStatusName, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    _ => items.OrderBy(x => x.Id) 
                }
            );

            return Results.Ok(result);
        });

        adminGroup.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var (adminId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (adminId == 0) return Results.Unauthorized();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, adminId);

            var dto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(u => u.Reseller)
                .Where(u => u.Id == id)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    IsReseller = u.IsReseller,
                    ResellerId = u.ResellerId,
                    ResellerName = u.Reseller != null ? u.Reseller.Name : null,
                    LanguageId = u.LanguageId,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == language)
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
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var language = await DbHelpers.GetLanguageByIdAsync(db, dto.LanguageId); 
            if (language == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.language_not_found" });
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                IsAdmin = dto.IsAdmin,
                IsReseller = dto.IsReseller,
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
            if (!ValidationHelpers.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }
            var user = await DbHelpers.GetUserByIdAsync(db, id);
            if (user == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            var language = await DbHelpers.GetLanguageByIdAsync(db, updatedDto.LanguageId); 
            if (language == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.language_not_found" });
            }

            user.Name = updatedDto.Name;
            user.Email = updatedDto.Email;
            user.IsAdmin = updatedDto.IsAdmin;
            user.IsReseller = updatedDto.IsReseller;
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