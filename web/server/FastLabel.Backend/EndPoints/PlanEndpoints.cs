namespace FastLabel.Backend.Endpoints;

public static class PlanEndpoints
{
    public static void MapPlanEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/plans");

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            var (_, language) = userId > 0
                ? await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId)
                : await DbHelpers.GetLanguageAsync(httpContext, db);

            int activeStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");

            var plans = await db.Plans
                .Include(p => p.Name)
                    .ThenInclude(t => t!.Translations)
                        .ThenInclude(tr => tr.Language)
                .Where(p => p.BasicStatusId == activeStatusId)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name != null
                        ? (p.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Name.DefaultTranslation)
                        : string.Empty,
                    Description = null,
                    SortOrder = p.SortOrder,
                    IsActive = p.BasicStatusId == activeStatusId,
                    MonthlyPrice = p.MonthlyPrice,
                    YearlyPrice = p.YearlyPrice,
                    MonthlyPrintsIncluded = p.MonthlyPrintsIncluded,
                    TopUpPrintsIncluded = p.TopUpPrintsIncluded,
                    TopUpPrice = p.TopUpPrice,
                    StripeMonthlyPriceId = p.StripeMonthlyPriceId,
                    StripeYearlyPriceId = p.StripeYearlyPriceId,
                    StripeTopUpPriceId = p.StripeTopUpPriceId
                })
                .ToListAsync();

            return Results.Ok(plans);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            var (_, language) = userId > 0
                ? await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId)
                : await DbHelpers.GetLanguageAsync(httpContext, db);

            int activeStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");

            var plan = await db.Plans
                .Include(p => p.Name)
                    .ThenInclude(t => t!.Translations)
                        .ThenInclude(tr => tr.Language)
                .Where(p => p.Id == id)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name != null
                        ? (p.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Name.DefaultTranslation)
                        : string.Empty,
                    Description = null,
                    SortOrder = p.SortOrder,
                    IsActive = p.BasicStatusId == activeStatusId,
                    MonthlyPrice = p.MonthlyPrice,
                    YearlyPrice = p.YearlyPrice,
                    MonthlyPrintsIncluded = p.MonthlyPrintsIncluded,
                    TopUpPrintsIncluded = p.TopUpPrintsIncluded,
                    TopUpPrice = p.TopUpPrice,
                    StripeMonthlyPriceId = p.StripeMonthlyPriceId,
                    StripeYearlyPriceId = p.StripeYearlyPriceId,
                    StripeTopUpPriceId = p.StripeTopUpPriceId
                })
                .FirstOrDefaultAsync();

            return plan is not null
                ? Results.Ok(plan)
                : Results.NotFound(new ErrorResponseDto { Message = "plan.error.not_found" });
        });

        group.MapGet("/by-code/{code}", async (string code, AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            var (_, language) = userId > 0
                ? await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId)
                : await DbHelpers.GetLanguageAsync(httpContext, db);

            int activeStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");

            var plan = await db.Plans
                .Include(p => p.Name)
                    .ThenInclude(t => t!.Translations)
                        .ThenInclude(tr => tr.Language)
                .Where(p => p.Code == code)
                .Select(p => new PlanDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name != null
                        ? (p.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Name.DefaultTranslation)
                        : string.Empty,
                    Description = null,
                    SortOrder = p.SortOrder,
                    IsActive = p.BasicStatusId == activeStatusId,
                    MonthlyPrice = p.MonthlyPrice,
                    YearlyPrice = p.YearlyPrice,
                    MonthlyPrintsIncluded = p.MonthlyPrintsIncluded,
                    TopUpPrintsIncluded = p.TopUpPrintsIncluded,
                    TopUpPrice = p.TopUpPrice,
                    StripeMonthlyPriceId = p.StripeMonthlyPriceId,
                    StripeYearlyPriceId = p.StripeYearlyPriceId,
                    StripeTopUpPriceId = p.StripeTopUpPriceId
                })
                .FirstOrDefaultAsync();

            return plan is not null
                ? Results.Ok(plan)
                : Results.NotFound(new ErrorResponseDto { Message = "plan.error.not_found" });
        });
    }
}