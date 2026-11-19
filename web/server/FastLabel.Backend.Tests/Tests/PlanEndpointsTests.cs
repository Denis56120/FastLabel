namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class PlanEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    [Fact]
    public async Task GetPlans_EndToEnd_ReturnsActivePlansOrdered()
    {
        var response = await _client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.NotEmpty(plans);
        Assert.All(plans, p => Assert.True(p.IsActive));
        Assert.True(plans.SequenceEqual(plans.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)));
    }

    [Fact]
    public async Task GetPlans_EndToEnd_ExcludesInactivePlans()
    {
        string inactiveCode;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inactiveStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.inactive");
            var sourcePlan = await db.Plans.OrderBy(p => p.Id).FirstAsync();
            inactiveCode = $"inactive_{Guid.NewGuid():N}";

            db.Plans.Add(new Plan
            {
                Code = inactiveCode,
                NameTextId = sourcePlan.NameTextId,
                SortOrder = 999,
                BasicStatusId = inactiveStatusId,
                MonthlyPrice = 99m,
                YearlyPrice = 990m,
                MonthlyPrintsIncluded = 1000
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.DoesNotContain(plans, p => p.Code == inactiveCode);
    }

    [Fact]
    public async Task GetPlanById_EndToEnd_ReturnsPlan()
    {
        int planId;
        string expectedCode;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = await db.Plans.OrderBy(p => p.Id).FirstAsync();
            planId = plan.Id;
            expectedCode = plan.Code;
        }

        var response = await _client.GetAsync($"/api/plans/{planId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlanDto>();
        Assert.NotNull(dto);
        Assert.Equal(planId, dto.Id);
        Assert.Equal(expectedCode, dto.Code);
    }

    [Fact]
    public async Task GetPlanById_Fail_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/plans/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("plan.error.not_found", error?.Message);
    }

    [Fact]
    public async Task GetPlanByCode_EndToEnd_ReturnsPlan()
    {
        string code;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = await db.Plans.OrderBy(p => p.Id).FirstAsync();
            code = plan.Code;
        }

        var response = await _client.GetAsync($"/api/plans/by-code/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlanDto>();
        Assert.NotNull(dto);
        Assert.Equal(code, dto.Code);
    }

    [Fact]
    public async Task GetPlanByCode_Fail_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/plans/by-code/unknown_plan_code");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("plan.error.not_found", error?.Message);
    }

    [Fact]
    public async Task GetPlans_EndToEnd_UsesAuthenticatedUserLanguage()
    {
        var email = "plan.lang@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);

        string customTag = $"plan.test.{Guid.NewGuid():N}.name";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var frLang = await db.Languages.FirstOrDefaultAsync(l => l.Code == "fr")
                ?? await db.Languages.OrderBy(l => l.Id).FirstAsync();

            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.LanguageId = frLang.Id;

            var activeStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");
            var text = new Text
            {
                Tag = customTag,
                DefaultTranslation = "Default Plan"
            };
            db.Texts.Add(text);
            await db.SaveChangesAsync();

            db.Plans.Add(new Plan
            {
                Code = $"plan_fr_{Guid.NewGuid():N}",
                NameTextId = text.Id,
                SortOrder = 50,
                BasicStatusId = activeStatusId,
                MonthlyPrice = 10m,
                YearlyPrice = 100m,
                MonthlyPrintsIncluded = 500
            });

            var localized = await DbHelpers.GetLocalizedTextsAsync(db, frLang.Code, customTag);
            Assert.NotNull(localized);

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.NotEmpty(plans);
    }
}