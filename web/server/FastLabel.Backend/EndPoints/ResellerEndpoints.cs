namespace FastLabel.Backend.Endpoints;

public static class ResellerEndpoints
{
    public static void MapResellerEndpoints(this IEndpointRouteBuilder routes)
    {
        var resellerGroup = routes.MapGroup("/api/reseller").RequireAuthorization();

        // 1. Lister les clients finaux gérés par le revendeur connecté (avec support du tri et des filtres)
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
            var resellerIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(resellerIdString, out var resellerId))
                return Results.Unauthorized();

            var reseller = await db.Users.FindAsync(resellerId);
            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            int currentPage = page ?? 1;
            int size = pageSize ?? 10;

            var query = db.Users.Where(u => u.ResellerId == resellerId).AsQueryable();

            // Application des filtres textuels
            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(u => u.Name != null && u.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByEmail))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(filterByEmail));
            }

            int totalCount = await query.CountAsync();

            // Application du tri dynamique
            bool ascending = sortAscending ?? true;
            query = sortBy?.ToLower() switch
            {
                "name" => ascending ? query.OrderBy(u => u.Name) : query.OrderByDescending(u => u.Name),
                "email" => ascending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
                "createdat" => ascending ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
                _ => query.OrderBy(u => u.Id) // Tri par défaut
            };

            var items = await query
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    IsReseller = u.IsReseller,
                    ResellerId = u.ResellerId,
                    LanguageId = u.LanguageId,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            var pagedResult = new PagedResultDto<UserDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = currentPage,
                PageSize = size
            };

            return Results.Ok(pagedResult);
        });

        // 2. Ajouter un client final (Incrémente Stripe + Crée le compte inactif + Envoie l'e-mail d'activation multilingue)
        resellerGroup.MapPost("/clients", async (UserDto clientDto, AppDbContext db, IEmailService emailService, IConfiguration configuration, IStripeService stripeService, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var resellerIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(resellerIdString, out var resellerId))
                return Results.Unauthorized();

            var reseller = await db.Users
                .Include(u => u.StripeRecord)
                .FirstOrDefaultAsync(u => u.Id == resellerId);

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            if (!ValidationExtensions.TryValidateModel(clientDto, out var errorResult))
            {
                return errorResult!;
            }

            bool emailExists = await db.Users.AnyAsync(u => u.Email == clientDto.Email);
            if (emailExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.email_already_taken" });
            }

            // Mettre à jour l'abonnement Stripe du revendeur (+1 quantité)
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

            // Récupérer le statut "inactive" par défaut pour l'invitation
            var inactiveStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.inactive")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstOrDefaultAsync();

            var activationToken = Guid.NewGuid().ToString();

            var newClient = new User
            {
                Name = clientDto.Name,
                Email = clientDto.Email,
                LanguageId = reseller.LanguageId,
                BasicStatusId = inactiveStatus?.Id ?? 1,
                ResellerId = resellerId,
                IsReseller = false,
                EmailVerificationToken = activationToken,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newClient);
            await db.SaveChangesAsync();

            // Construction du lien d'activation (identique au register)
            var serverUrl = configuration["Url:ServerUrl"] ?? "https://fastlabel.com";
            var verificationLink = $"{serverUrl}/api/auth/verify-email?token={activationToken}";

            // Récupération de la langue (via le cookie ou celle du revendeur)
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
                        ?? "Activate your FastLabel account";

            var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                            ?? "Hello {0},\n\nPlease click the following link to activate your account:\n{1}";

            var message = string.Format(bodyTemplate, newClient.Name, verificationLink);

            await emailService.SendEmailAsync(newClient.Email, subject, message);

            clientDto.Id = newClient.Id;
            clientDto.Password = null;

            return Results.Created($"/api/reseller/clients/{newClient.Id}", clientDto);
        });

        // 3. Supprimer un client final (Décrémente Stripe + Supprime le User)
        resellerGroup.MapDelete("/clients/{id:int}", async (int id, AppDbContext db, IStripeService stripeService, ClaimsPrincipal claimsUser) =>
        {
            var resellerIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(resellerIdString, out var resellerId))
                return Results.Unauthorized();

            var reseller = await db.Users
                .Include(u => u.StripeRecord)
                .FirstOrDefaultAsync(u => u.Id == resellerId);

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            var clientToDelete = await db.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.ResellerId == resellerId);

            if (clientToDelete == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            // Mettre à jour l'abonnement Stripe du revendeur (-1 quantité)
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