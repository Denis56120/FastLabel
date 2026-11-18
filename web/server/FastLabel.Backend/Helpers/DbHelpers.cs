namespace FastLabel.Backend.Helpers;

public static class DbHelpers
{
    public static async Task<int> GetStatusIdByTagAsync(AppDbContext db, string tag)
    {
        var statusId = await db.BasicStatuses
            .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
            .Where(x => x.Tag == tag)
            .OrderBy(x => x.bs.Id)
            .Select(x => (int?)x.bs.Id)
            .FirstOrDefaultAsync();

        if (statusId.HasValue)
        {
            return statusId.Value;
        }

        var fallbackStatus = await db.BasicStatuses.OrderBy(s => s.Id).Skip(1).FirstOrDefaultAsync()
                          ?? await db.BasicStatuses.OrderBy(s => s.Id).FirstOrDefaultAsync();

        return fallbackStatus?.Id ?? 1;
    }

    public static async Task<string> GetStatusTagByIdAsync(AppDbContext db, int id)
    {
        return await db.Texts
                .Join(db.BasicStatuses, t => t.Id, bs => bs.NameTextId, (t, bs) => new { bs.Id, t.Tag })
                .Where(x => x.Id == id)
                .OrderBy(x => x.Id)
                .Select(x => x.Tag)
                .FirstOrDefaultAsync() ?? string.Empty;
    }

    public static async Task<AgentType?> GetAgentTypeByIdAsync(AppDbContext db, int id)
    {
        return await db.AgentTypes.FindAsync(id);
    }

    public static async Task<Agent?> GetAgentByIdAsync(AppDbContext db, int id)
    {
        return await db.Agents.FindAsync(id);
    }

    public static async Task<Protocol?> GetProtocolByIdAsync(AppDbContext db, int id)
    {
        return await db.Protocols.FindAsync(id);
    }

    public static async Task<ResolutionDpi?> GetResolutionDpiByIdAsync(AppDbContext db, int id)
    {
        return await db.ResolutionDpis.FindAsync(id);
    }

    public static async Task<Template?> GetTemplateByIdAsync(AppDbContext db, int id)
    {
        return await db.Templates.FindAsync(id);
    }
    
    public static async Task<Printer?> GetPrinterByIdAsync(AppDbContext db, int id)
    {
        return await db.Printers.FindAsync(id);
    }

    public static async Task<User?> GetUserByIdAsync(AppDbContext db, int id)
    {
        return await db.Users.FindAsync(id);
    }

    public static async Task<Language?> GetLanguageByIdAsync(AppDbContext db, int id)
    {
        return await db.Languages.FindAsync(id);
    }

    public static async Task<User?> GetUserByIdWithStripeRecordAsync(AppDbContext db, int id)
    {
        return await db.Users
                .Include(u => u.StripeRecord)
                .FirstOrDefaultAsync(u => u.Id == id);
    }

    public static async Task<Plan?> GetPlanByTagAsync(AppDbContext db, string tag)
    {
        return await db.Plans
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name.Tag == tag);
    }

    public static async Task<User?> GetUserByIdAndResellerIdAsync(AppDbContext db, int id, int resellerId)
    {
        return await db.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.ResellerId == resellerId);
    }

    public static async Task<Agent?> GetAgentByApiKeyAsync(AppDbContext db, string ApiKey)
    {
        return await db.Agents.FirstOrDefaultAsync(l => l.ApiKey == ApiKey);
    }

    public static async Task<Language?> GetLanguageByCodeAsync(AppDbContext db, string code)
    {
        return await db.Languages.FirstOrDefaultAsync(l => l.Code == code);
    }

    public static async Task<User?> GetUserByEmailAsync(AppDbContext db, string email)
    {
        return await db.Users
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(u => u.Email == email);
    }

    public static async Task<User?> GetUserByPasswordResetTokenAsync(AppDbContext db, string token)
    {
        return await db.Users
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token);
    }

    public static async Task<User?> GetUserByEmailVerificationTokenAsync(AppDbContext db, string token)
    {
        return await db.Users
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
    }

    public static async Task<(int Id, string Code)> GetUserLanguageByUserClaimAsync(HttpContext httpContext, AppDbContext db, ClaimsPrincipal claim)
    {
        var userIdString = ClaimsPrincipalHelpers.GetUserIdByClaimPrincipal(claim);

        if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
        {
            return await GetUserLanguageByUserIdAsync(httpContext, db, userId);
        }

        return await GetLanguageAsync(httpContext, db);
    }
    
    public static async Task<(int Id, string Code)> GetUserLanguageByUserIdAsync(HttpContext httpContext, AppDbContext db, int id)
    {
        var langInfo = await db.Users
            .Where(u => u.Id == id)
            .Select(u => u.Language != null ? new { u.Language.Id, u.Language.Code } : null)
            .FirstOrDefaultAsync();

        if (langInfo != null)
        {
            return (langInfo.Id, langInfo.Code);
        }

        return await GetLanguageAsync(httpContext, db);
    }

    public static async Task<(int Id, string Code)> GetLanguageAsync(HttpContext httpContext, AppDbContext db)
    {
        string language = HttpContextHelpers.DefaultLanguage;
        if (httpContext.Request.Cookies.TryGetValue("language", out var cookieLang) && !string.IsNullOrEmpty(cookieLang))
        {
            language = cookieLang;
        }

        var targetLanguage = await db.Languages.FirstOrDefaultAsync(l => l.Code == language)
                          ?? await db.Languages.FirstOrDefaultAsync(l => l.Code == HttpContextHelpers.DefaultLanguage)
                          ?? await db.Languages.OrderBy(l => l.Id).FirstOrDefaultAsync();

        return (targetLanguage?.Id ?? 1, targetLanguage?.Code ?? HttpContextHelpers.DefaultLanguage);
    }
}