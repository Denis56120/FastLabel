namespace FastLabel.Backend.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/account/subscriptions").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var userIdString = ClaimsPrincipalHelpers.GetUserIdByClaimPrincipal(claimsUser);

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            bool isAdmin = claimsUser.IsInRole("Admin");

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

            var query = db.Subscriptions
                .Include(s => s.Plan)
                    .ThenInclude(p => p.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .AsQueryable()
                .Where(s => s.UserId == userId);

            var subscriptions = await query
                .OrderBy(s => s.Id)
                .Select(s => new SubscriptionDto
                {
                    Id = s.Id,
                    PlanName = s.Plan != null && s.Plan.Name != null
                        ? (s.Plan.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? s.Plan.Name.DefaultTranslation)
                        : string.Empty,
                    SubscriptionPeriod = s.SubscriptionPeriod,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(subscriptions);
        });

        group.MapGet("/current", async (AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var userIdString = ClaimsPrincipalHelpers.GetUserIdByClaimPrincipal(claimsUser);

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

            var now = DateTime.UtcNow;

            var userSubscriptions = await db.Subscriptions
                .Include(s => s.Plan)
                    .ThenInclude(p => p.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var currentSub = userSubscriptions
                .Where(s =>
                    (s.Plan?.Code == "free" || s.SubscriptionPeriod == 0) ||
                    ((s.CurrentPeriodEnd ?? s.CreatedAt.AddMonths(s.SubscriptionPeriod)) > now))
                .OrderByDescending(s => s.SubscriptionPeriod > 0)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            if (currentSub == null)
                return Results.Ok(new SubscriptionDto());

            var subscriptionDto = new SubscriptionDto
            {
                Id = currentSub.Id,
                PlanCode = currentSub.Plan?.Code ?? "free",
                PlanName = currentSub.Plan?.Name != null
                    ? (currentSub.Plan.Name.Translations
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault()
                    ?? currentSub.Plan.Name.DefaultTranslation)
                    : string.Empty,
                SubscriptionPeriod = currentSub.SubscriptionPeriod,
                MonthlyPrice = currentSub.Plan?.MonthlyPrice ?? 0m,
                YearlyPrice = currentSub.Plan?.YearlyPrice ?? 0m,
                MonthlyPrintsIncluded = currentSub.Plan?.MonthlyPrintsIncluded ?? 0,
                ExtraPrintsBalance = currentSub.ExtraPrintsBalance,
                TopUpPrintsIncluded = currentSub.Plan?.TopUpPrintsIncluded,
                TopUpPrice = currentSub.Plan?.TopUpPrice,
                CurrentPeriodStart = currentSub.CurrentPeriodStart ?? currentSub.CreatedAt,
                CurrentPeriodEnd = currentSub.CurrentPeriodEnd ?? (currentSub.SubscriptionPeriod > 0
                    ? currentSub.CreatedAt.AddMonths(currentSub.SubscriptionPeriod)
                    : null),
                HasStripeSubscription = currentSub.StripeId.HasValue,
                CreatedAt = currentSub.CreatedAt
            };

            return Results.Ok(subscriptionDto);
        });
    }
}