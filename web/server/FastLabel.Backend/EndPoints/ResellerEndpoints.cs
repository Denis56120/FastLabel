namespace FastLabel.Backend.Endpoints;

public static class ResellerEndpoints
{
    public static void MapResellerEndpoints(this IEndpointRouteBuilder routes)
    {
        var resellerGroup = routes.MapGroup("/api/reseller").RequireAuthorization();

        resellerGroup.MapGet("/clients", async (
            int? page, 
            int? pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByName,
            string? filterByEmail,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdAsync(db, userId);  
            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            int currentPage = page ?? 1;
            int size = pageSize ?? 10;

            var query = db.Users.Where(u => u.ResellerId == userId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(u => u.Name != null && u.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByEmail))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(filterByEmail));
            }

            var naturalComparer = new NaturalStringHelpers();

            var result = await query.ToPagedResultAsync(
                currentPage,
                size,
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
                    LanguageId = u.LanguageId,
                    CreatedAt = u.CreatedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "name" => ascending ? items.OrderBy(x => x.Name, naturalComparer) : items.OrderByDescending(x => x.Name, naturalComparer),
                    "email" => ascending ? items.OrderBy(x => x.Email, naturalComparer) : items.OrderByDescending(x => x.Email, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    _ => items.OrderBy(x => x.Id)
                }
            );

            return Results.Ok(result);
        });

        resellerGroup.MapPost("/clients", async (UserDto clientDto, AppDbContext db, IEmailService emailService, IConfiguration configuration, IStripeService stripeService, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdWithStripeRecordAsync(db, userId);  

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            if (!ValidationHelpers.TryValidateModel(clientDto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await DbHelpers.GetUserByEmailAsync(db, clientDto.Email); 
            if (user != null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.email_already_taken" });
            }

            if (reseller.StripeRecord != null && !string.IsNullOrEmpty(reseller.StripeRecord.StripeSubscriptionId))
            {
                try
                {
                    await stripeService.IncrementSubscriptionQuantityAsync(reseller.StripeRecord.StripeSubscriptionId);
                }
                catch (Exception)
                {
                    return Results.BadRequest(new ErrorResponseDto { Message = "reseller.error.stripe_update_failed" });
                }
            }

            int inactiveStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.inactive");

            var activationToken = Guid.NewGuid().ToString();

            var newClient = new User
            {
                Name = clientDto.Name,
                Email = clientDto.Email,
                LanguageId = reseller.LanguageId,
                BasicStatusId = inactiveStatusId,
                ResellerId = userId,
                IsReseller = false,
                EmailVerificationToken = activationToken,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newClient);
            await db.SaveChangesAsync();

            var serverUrl = ConfigurationHelpers.GetServerUrl(configuration);
            var verificationLink = $"{serverUrl}/api/auth/verify-email?token={activationToken}";

            var (languageId, language) = await DbHelpers.GetLanguageAsync(httpContext, db);

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
                        ?? "Activate your FastLabel account";

            var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                            ?? "Hello {0},\n\nPlease click the following link to activate your account:\n{1}";
            var message = string.Format(bodyTemplate, newClient.Name, verificationLink);

            await emailService.SendEmailAsync(newClient.Email, subject, message);

            clientDto.Id = newClient.Id;
            clientDto.Password = null;

            return Results.Created($"/api/reseller/clients/{newClient.Id}", clientDto);
        });

        resellerGroup.MapDelete("/clients/{id:int}", async (int id, AppDbContext db, IStripeService stripeService, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdWithStripeRecordAsync(db, userId);  

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            var clientToDelete = await DbHelpers.GetUserByIdAndResellerIdAsync(db, id, userId);  
            if (clientToDelete == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            if (reseller.StripeRecord != null && !string.IsNullOrEmpty(reseller.StripeRecord.StripeSubscriptionId))
            {
                try
                {
                    await stripeService.DecrementSubscriptionQuantityAsync(reseller.StripeRecord.StripeSubscriptionId);
                }
                catch (Exception)
                {
                    return Results.BadRequest(new ErrorResponseDto { Message = "reseller.error.stripe_update_failed" });
                }
            }

            db.Users.Remove(clientToDelete);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}