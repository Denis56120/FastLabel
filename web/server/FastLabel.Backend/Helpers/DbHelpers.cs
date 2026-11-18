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
        var userIdString = claim.FindFirstValue(ClaimTypes.NameIdentifier);

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