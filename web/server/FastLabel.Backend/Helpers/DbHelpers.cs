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

    public static async Task<Plan?> GetPlanByPriceIdAsync(AppDbContext db, string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return null;
        }

        return await db.Plans.FirstOrDefaultAsync(p =>
            p.StripeMonthlyPriceId == priceId ||
            p.StripeYearlyPriceId == priceId ||
            p.StripeTopUpPriceId == priceId);
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

    public static async Task<int> GetPlanIdByCodeAsync(AppDbContext db, string code)
    {
        var planId = await db.Plans
            .Where(p => p.Code == code)
            .OrderBy(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();

        if (planId.HasValue)
        {
            return planId.Value;
        }

        var fallbackPlan = await db.Plans.OrderBy(p => p.Id).Skip(1).FirstOrDefaultAsync()
                        ?? await db.Plans.OrderBy(p => p.Id).FirstOrDefaultAsync();

        return fallbackPlan?.Id ?? 1;
    }

    public static async Task<int> GetAgentTypeIdByTagAsync(AppDbContext db, string tag)
    {
        var agentTypeId = await db.AgentTypes
            .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
            .Where(x => x.Tag == tag)
            .OrderBy(x => x.bs.Id)
            .Select(x => (int?)x.bs.Id)
            .FirstOrDefaultAsync();

        if (agentTypeId.HasValue)
        {
            return agentTypeId.Value;
        }

        var fallbackStatus = await db.AgentTypes.OrderBy(s => s.Id).Skip(1).FirstOrDefaultAsync()
                          ?? await db.AgentTypes.OrderBy(s => s.Id).FirstOrDefaultAsync();

        return fallbackStatus?.Id ?? 1;
    }

    public static async Task<int> GetProtocolIdByTagAsync(AppDbContext db, string tag)
    {
        var protocolId = await db.Protocols
            .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
            .Where(x => x.Tag == tag)
            .OrderBy(x => x.bs.Id)
            .Select(x => (int?)x.bs.Id)
            .FirstOrDefaultAsync();

        if (protocolId.HasValue)
        {
            return protocolId.Value;
        }

        var fallbackStatus = await db.Protocols.OrderBy(s => s.Id).Skip(1).FirstOrDefaultAsync()
                          ?? await db.Protocols.OrderBy(s => s.Id).FirstOrDefaultAsync();

        return fallbackStatus?.Id ?? 1;
    }

    public static async Task<int> GetResolutionDpiIdByValueAsync(AppDbContext db, int value)
    {
        var resolutiondpi = await db.ResolutionDpis.FirstOrDefaultAsync(l => l.Value == value);
        return resolutiondpi?.Id ?? 1;
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

    public static async Task<Subscription> CreateSubscription(AppDbContext db, int userid, string plancode, int subscriptionperiod = 0, int? stripeid = null, DateTime? currentperiodstart = null, DateTime? currentperiodend = null, int extraprintsbalance = 0)
    {
        var freePlanId = await GetPlanIdByCodeAsync(db, plancode);

        return new Subscription
        {
            UserId = userid,
            PlanId = freePlanId,
            SubscriptionPeriod = subscriptionperiod,
            StripeId = stripeid,
            CurrentPeriodStart = currentperiodstart,
            CurrentPeriodEnd = currentperiodend,
            ExtraPrintsBalance = extraprintsbalance,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static async Task<Agent> CreateAgent(AppDbContext db, int userid, string agenttypetag)
    {
        int agentTypeId = await GetAgentTypeIdByTagAsync(db, agenttypetag);

        return new Agent
        {
            UserId = userid,
            AgentTypeId = agentTypeId,
            ApiKey = $"fl_{Guid.NewGuid():N}{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static async Task<Printer> CreatePrinter(AppDbContext db, int userid, string name = "Test", string location = "", string ip = "192.168.1.1", int port = 9100, string templatetag = "protocol.rasterized_zpl", int resolutiondpivalue = 300)
    {
        int statusId = await GetStatusIdByTagAsync(db, "status.active");
        int protocolId = await GetProtocolIdByTagAsync(db, templatetag);
        int resolutionDpiId = await GetResolutionDpiIdByValueAsync(db, resolutiondpivalue);

        return new Printer
        {
            UserId = userid,
            Name = name,
            Location = location,
            IP = ip,
            Port = port,
            ProtocolId = protocolId,
            ResolutionDpiId = resolutionDpiId,
            BasicStatusId = statusId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static async Task<Template> CreateTemplate(AppDbContext db, int userid, string name = "Test")
    {
        int statusId = await GetStatusIdByTagAsync(db, "status.active");

        return new Template
        {
            UserId = userid,
            Name = name,
            BasicStatusId = statusId,
            JsonData = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static async Task<Dictionary<string, string>> GetLocalizedTextsAsync(AppDbContext db, string languageCode, params string[] tags)
    {
        return await db.Texts
            .Where(t => tags.Contains(t.Tag))
            .Select(t => new
            {
                t.Tag,
                Value = t.Translations
                    .Where(tr => tr.Language.Code == languageCode)
                    .Select(tr => tr.TranslatedValue)
                    .FirstOrDefault() ?? t.DefaultTranslation
            })
            .ToDictionaryAsync(t => t.Tag, t => t.Value);
    }

    public static async Task CreateUserAndSendActivationEmailAsync(
        AppDbContext db,
        IEmailService emailService,
        IConfiguration configuration,
        User user,
        string languageCode)
    {
        var activationTexts = await GetLocalizedTextsAsync(db, languageCode, "templates.activation.name", "printers.activation.name", "email.activation.subject", "email.activation.body");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var freeSubscription = await CreateSubscription(db, user.Id, "free", 0, null, DateTime.UtcNow, null, 0);        
        db.Subscriptions.AddRange(freeSubscription); 

        var windowsAgent = await CreateAgent(db, user.Id, "agenttype.windows");
        var linuxAgent = await CreateAgent(db, user.Id, "agenttype.linux");
        db.Agents.AddRange(windowsAgent, linuxAgent);

        var templateName = activationTexts.GetValueOrDefault("templates.activation.name", string.Empty);
        var printerName = activationTexts.GetValueOrDefault("printers.activation.name", string.Empty);

        var testprinter = await CreatePrinter(db, user.Id, printerName);
        db.Printers.Add(testprinter);

        var testlabel = await CreateTemplate(db, user.Id, templateName);
        db.Templates.Add(testlabel);

        await db.SaveChangesAsync();

        var serverUrl = ConfigurationHelpers.GetServerUrl(configuration);
        var verificationLink = $"{serverUrl}/api/auth/verify-email?token={user.EmailVerificationToken}";

        var subject = activationTexts.GetValueOrDefault("email.activation.subject", string.Empty);
        var bodyTemplate = activationTexts.GetValueOrDefault("email.activation.body", string.Empty);

        var message = string.Format(bodyTemplate, user.Name, verificationLink);

        await emailService.SendEmailAsync(user.Email, subject, message);
    }
}