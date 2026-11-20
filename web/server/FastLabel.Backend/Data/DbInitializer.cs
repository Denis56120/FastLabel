namespace FastLabel.Backend.Data;

public class DbInitializer(AppDbContext context, IConfiguration configuration, IPasswordHasher<User>? passwordHasher, IWebHostEnvironment env)
{
    private readonly AppDbContext _context = context;
    private readonly IConfiguration _configuration = configuration;
    private IPasswordHasher<User>? _passwordHasher = passwordHasher;
    private readonly IWebHostEnvironment _env = env;

    public async Task InitializeAsync()
    {
        await SeedReferenceDataAsync();

        await SeedTranslationsAsync();
    }

    private async Task SyncTranslationsAsync(Dictionary<string, (string DefaultTranslation, Dictionary<string, string> Translations)> entries)
    {
        var languages = await _context.Languages
            .AsNoTracking()
            .ToDictionaryAsync(l => l.Code, l => l.Id);

        var existingTexts = await _context.Texts
            .Include(t => t.Translations)
            .ToListAsync();

        var textByTag = existingTexts
            .Where(t => !string.IsNullOrEmpty(t.Tag))
            .ToDictionary(t => t.Tag!, t => t, StringComparer.OrdinalIgnoreCase);

        bool hasChanges = false;

        foreach (var (tag, (defaultTranslation, translations)) in entries)
        {
            if (!textByTag.TryGetValue(tag, out var textEntity))
            {
                textEntity = new Text
                {
                    Tag = tag,
                    DefaultTranslation = defaultTranslation,
                    Translations = new List<Translation>()
                };
                _context.Texts.Add(textEntity);
                textByTag[tag] = textEntity;
                hasChanges = true;
            }
            else if (textEntity.DefaultTranslation != defaultTranslation)
            {
                textEntity.DefaultTranslation = defaultTranslation;
                hasChanges = true;
            }

            textEntity.Translations ??= new List<Translation>();

            foreach (var (langCode, translatedValue) in translations)
            {
                if (!languages.TryGetValue(langCode, out var languageId))
                    continue;

                var existingTranslation = textEntity.Translations
                    .FirstOrDefault(tr => tr.LanguageId == languageId);

                if (existingTranslation == null)
                {
                    textEntity.Translations.Add(new Translation
                    {
                        LanguageId = languageId,
                        TranslatedValue = translatedValue
                    });
                    hasChanges = true;
                }
                else if (existingTranslation.TranslatedValue != translatedValue)
                {
                    existingTranslation.TranslatedValue = translatedValue;
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<Text> CreateTextWithTranslationsAsync(string tag, string defaultEnglishVal, Dictionary<string, string> translations)
    {
        var textEntity = await _context.Texts
            .Include(t => t.Translations)
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(t => t.Tag == tag);

        if (textEntity == null)
        {
            textEntity = new Text
            {
                Tag = tag,
                DefaultTranslation = defaultEnglishVal
            };
            _context.Texts.Add(textEntity);
            await _context.SaveChangesAsync();
        }

        foreach (var kvp in translations)
        {
            var langCode = kvp.Key;
            var translatedValue = kvp.Value;

            var lang = await _context.Languages
                .OrderBy(l => l.Id)
                .FirstOrDefaultAsync(l => l.Code == langCode);
            if (lang != null)
            {
                var existingTranslation = textEntity.Translations
                    .FirstOrDefault(tr => tr.LanguageId == lang.Id);

                if (existingTranslation == null)
                {
                    _context.Translations.Add(new Translation
                    {
                        TextId = textEntity.Id,
                        LanguageId = lang.Id,
                        TranslatedValue = translatedValue
                    });
                }
                else if (existingTranslation.TranslatedValue != translatedValue)
                {
                    existingTranslation.TranslatedValue = translatedValue;
                }
            }
        }

        await _context.SaveChangesAsync();
        return textEntity;
    }

    private async Task SeedReferenceDataAsync()
    {
        // 1. Textes de description des langues
        var descFr = await CreateTextWithTranslationsAsync("lang.fr.desc", "French", new() { { "fr", "Français" }, { "en", "French" } });
        var descEn = await CreateTextWithTranslationsAsync("lang.en.desc", "English", new() { { "fr", "Anglais" }, { "en", "English" } });

        // 2. Langues
        if (!await _context.Languages.AnyAsync())
        {
            _context.Languages.AddRange(
                new Language { Code = "fr", DescriptionTextId = descFr.Id, Flag = "🇫🇷" },
                new Language { Code = "en", DescriptionTextId = descEn.Id, Flag = "🇬🇧" }
            );
            await _context.SaveChangesAsync();
        }

        var englishLanguage = await _context.Languages
            .FirstOrDefaultAsync(l => l.Code == "en")
            ?? throw new InvalidOperationException("La langue anglaise ('en') est introuvable.");
        int englishLanguageId = englishLanguage.Id;

        // 3. Protocoles d'impression
        if (!await _context.Protocols.AnyAsync())
        {
            var p1 = await CreateTextWithTranslationsAsync("protocol.rasterized_zpl", "Hybrid (ZPL + Raster)", new() { { "fr", "Hybride (ZPL + Raster)" }, { "en", "Hybrid (ZPL + Raster)" } });
            var p2 = await CreateTextWithTranslationsAsync("protocol.native_zpl", "Native ZPL", new() { { "fr", "ZPL Natif" }, { "en", "Native ZPL" } });

            _context.Protocols.AddRange(
                new Protocol { NameTextId = p1.Id },
                new Protocol { NameTextId = p2.Id }
            );
            await _context.SaveChangesAsync();
        }

        // 4. Résolutions DPI
        if (!await _context.ResolutionDpis.AnyAsync())
        {
            _context.ResolutionDpis.AddRange(
                new ResolutionDpi { Value = 203 },
                new ResolutionDpi { Value = 300 },
                new ResolutionDpi { Value = 600 }
            );
            await _context.SaveChangesAsync();
        }

        // 5. Types d'agents
        if (!await _context.AgentTypes.AnyAsync())
        {
            var a1 = await CreateTextWithTranslationsAsync("agenttype.windows", "Windows Agent", new() { { "fr", "Agent Windows" }, { "en", "Windows Agent" } });
            var a2 = await CreateTextWithTranslationsAsync("agenttype.linux", "Linux Agent", new() { { "fr", "Agent Linux" }, { "en", "Linux Agent" } });

            _context.AgentTypes.AddRange(
                new AgentType { NameTextId = a1.Id },
                new AgentType { NameTextId = a2.Id }
            );
            await _context.SaveChangesAsync();
        }

        // 6. Statuts de base
        if (!await _context.BasicStatuses.AnyAsync())
        {
            var s1 = await CreateTextWithTranslationsAsync("status.active", "Active", new() { { "fr", "Actif" }, { "en", "Active" } });
            var s2 = await CreateTextWithTranslationsAsync("status.inactive", "Inactive", new() { { "fr", "Inactif" }, { "en", "Inactive" } });

            _context.BasicStatuses.AddRange(
                new BasicStatus { NameTextId = s1.Id },
                new BasicStatus { NameTextId = s2.Id }
            );
            await _context.SaveChangesAsync();
        }

        var statusTextId = await _context.Texts
            .Where(t => t.Tag == "status.active")
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Le texte avec le tag 'status.active' est introuvable.");

        var activeStatus = await _context.BasicStatuses
            .FirstOrDefaultAsync(s => s.NameTextId == statusTextId)
            ?? throw new InvalidOperationException("Le BasicStatus correspondant au tag 'status.active' est introuvable.");

        int activeStatusId = activeStatus.Id;

        // 7. Statuts des tâches d'impression
        if (!await _context.PrintJobStatuses.AnyAsync())
        {
            var j1 = await CreateTextWithTranslationsAsync("jobstatus.created", "Created", new() { { "fr", "Créé" }, { "en", "Created" } });
            var j2 = await CreateTextWithTranslationsAsync("jobstatus.inprogress", "In Progress", new() { { "fr", "En Cours" }, { "en", "In Progress" } });
            var j3 = await CreateTextWithTranslationsAsync("jobstatus.completed", "Completed", new() { { "fr", "Terminé" }, { "en", "Completed" } });
            var j4 = await CreateTextWithTranslationsAsync("jobstatus.suspended", "Suspended", new() { { "fr", "Suspendu" }, { "en", "Suspended" } });

            _context.PrintJobStatuses.AddRange(
                new PrintJobStatus { NameTextId = j1.Id },
                new PrintJobStatus { NameTextId = j2.Id },
                new PrintJobStatus { NameTextId = j3.Id },
                new PrintJobStatus { NameTextId = j4.Id }
            );
            await _context.SaveChangesAsync();
        }

        // 8. Plans d'abonnement
        if (!await _context.Plans.AnyAsync())
        {
            var planFreeText = await CreateTextWithTranslationsAsync("plan.free", "Free", new() { { "fr", "Gratuit" }, { "en", "Free" } });
            var planStarterText = await CreateTextWithTranslationsAsync("plan.starter", "Starter", new() { { "fr", "Starter" }, { "en", "Starter" } });
            var planProText = await CreateTextWithTranslationsAsync("plan.pro", "Pro", new() { { "fr", "Pro" }, { "en", "Pro" } });
            var planBusinessText = await CreateTextWithTranslationsAsync("plan.business", "Business", new() { { "fr", "Business" }, { "en", "Business" } });
            var planEnterpriseText = await CreateTextWithTranslationsAsync("plan.enterprise", "Enterprise", new() { { "fr", "Entreprise" }, { "en", "Enterprise" } });

            _context.Plans.AddRange(
                new Plan
                {
                    Code = "free",
                    NameTextId = planFreeText.Id,
                    MonthlyPrintsIncluded = 10,
                    MonthlyPrice = 0m,
                    YearlyPrice = 0m,
                    TopUpPrintsIncluded = null,
                    TopUpPrice = null,
                    SortOrder = 1,
                    BasicStatusId = activeStatusId
                },
                new Plan
                {
                    Code = "starter",
                    NameTextId = planStarterText.Id,
                    MonthlyPrintsIncluded = 500,
                    MonthlyPrice = 49m,
                    YearlyPrice = 470m,
                    TopUpPrintsIncluded = 100,
                    TopUpPrice = 9m,
                    StripeProductId = _env.IsProduction() ? "prod_TODO" : "prod_VcjVk4ebwZmmYQ",
                    StripeMonthlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIRE56MInqLcWx3aKllRRc",
                    StripeYearlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UbU2I56MInqLcWxBjLHwsC1",
                    StripeTopUpPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIVt56MInqLcWxFjESFNhN",
                    SortOrder = 2,
                    BasicStatusId = activeStatusId
                },
                new Plan
                {
                    Code = "pro",
                    NameTextId = planProText.Id,
                    MonthlyPrintsIncluded = 1_500,
                    MonthlyPrice = 79m,
                    YearlyPrice = 760m,
                    TopUpPrintsIncluded = 1_000,
                    TopUpPrice = 29m,
                    StripeProductId = _env.IsProduction() ? "prod_TODO" : "prod_VcjWw4zl5axAoC",
                    StripeMonthlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIQT56MInqLcWxEOpZCNQ3",
                    StripeYearlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UbU3I56MInqLcWxAmM5Esyz",
                    StripeTopUpPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIVe56MInqLcWxtjBQnaVF",
                    SortOrder = 3,
                    BasicStatusId = activeStatusId
                },
                new Plan
                {
                    Code = "business",
                    NameTextId = planBusinessText.Id,
                    MonthlyPrintsIncluded = 5_000,
                    MonthlyPrice = 199m,
                    YearlyPrice = 1_910m,
                    TopUpPrintsIncluded = 1_000,
                    TopUpPrice = 29m,
                    StripeProductId = _env.IsProduction() ? "prod_TODO" : "prod_VcjWdgZ48O14mq",
                    StripeMonthlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIPr56MInqLcWxPbohBHz2",
                    StripeYearlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UbU3o56MInqLcWxyyLSYWl6",
                    StripeTopUpPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIVQ56MInqLcWx5aoG2Izx",
                    SortOrder = 4,
                    BasicStatusId = activeStatusId
                },
                new Plan
                {
                    Code = "enterprise",
                    NameTextId = planEnterpriseText.Id,
                    MonthlyPrintsIncluded = 15_000,
                    MonthlyPrice = 399m,
                    YearlyPrice = 3_830m,
                    TopUpPrintsIncluded = 1_000,
                    TopUpPrice = 19m,
                    StripeProductId = _env.IsProduction() ? "prod_TODO" : "prod_VebfYZRXulp2Bb",
                    StripeMonthlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdISC56MInqLcWxv7gJsot1",
                    StripeYearlyPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIRp56MInqLcWxsI4rdLrz",
                    StripeTopUpPriceId = _env.IsProduction() ? "price_TODO" : "price_1UdIV656MInqLcWxgUWWTSc9",
                    SortOrder = 5,
                    BasicStatusId = activeStatusId
                }
            );

            await _context.SaveChangesAsync();
        }

        // 9. Compte Administrateur par défaut
        if (!await _context.Users.AnyAsync() && _env.IsProduction())
        {
            var adminName = _configuration["ADMIN_USER_NAME"];
            var adminEmail = _configuration["ADMIN_USER_EMAIL"];
            var adminPassword = _configuration["ADMIN_USER_PASSWORD"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException("ADMIN_USER_EMAIL et ADMIN_USER_PASSWORD doivent être définis pour créer le compte administrateur initial.");
            }

            _passwordHasher ??= new PasswordHasher<User>();

            var adminUser = new User
            {
                Name = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName.Trim(),
                Email = adminEmail.Trim(),
                BasicStatusId = activeStatusId,
                LanguageId = englishLanguageId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true,
                IsReseller = false
            };
            adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, adminPassword);

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            var subscription = await DbHelpers.CreateSubscription(_context, adminUser.Id, "free", 0, null, DateTime.UtcNow, null, 0);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedTranslationsAsync()
    {
        var translations = new Dictionary<string, (string DefaultTranslation, Dictionary<string, string> Translations)>
        {
            // 1. UI
            ["admin.home.title"] = ("Administration", new() { { "fr", "Administration" }, { "en", "Administration" } }),
            ["admin.home.refresh"] = ("Refresh", new() { { "fr", "Actualiser" }, { "en", "Refresh" } }),
            ["admin.home.refreshed"] = ("Home refreshed", new() { { "fr", "Accueil actualisé" }, { "en", "Home refreshed" } }),
            ["admin.status.connected"] = ("Connected", new() { { "fr", "Connecté" }, { "en", "Connected" } }),
            ["admin.metrics.ms"] = ("ms", new() { { "fr", "ms" }, { "en", "ms" } }),
            ["admin.metrics.users"] = ("Users", new() { { "fr", "Utilisateurs" }, { "en", "Users" } }),
            ["admin.metrics.resellers"] = ("Resellers", new() { { "fr", "Revendeurs" }, { "en", "Resellers" } }),
            ["admin.metrics.subscriptions"] = ("Subscriptions", new() { { "fr", "Abonnements" }, { "en", "Subscriptions" } }),
            ["admin.metrics.agents_active_24h"] = ("Active agents (24h)", new() { { "fr", "Agents actifs (24h)" }, { "en", "Active agents (24h)" } }),
            ["admin.metrics.templates"] = ("Templates", new() { { "fr", "Modèles" }, { "en", "Templates" } }),
            ["admin.metrics.printers"] = ("Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } }),
            ["admin.print.section_title"] = ("Print activity", new() { { "fr", "Activité d'impression" }, { "en", "Print activity" } }),
            ["admin.print.period"] = ("Period", new() { { "fr", "Période" }, { "en", "Period" } }),
            ["admin.print.jobs"] = ("Jobs", new() { { "fr", "Jobs" }, { "en", "Jobs" } }),
            ["admin.print.labels"] = ("Labels", new() { { "fr", "Étiquettes" }, { "en", "Labels" } }),
            ["admin.print.last_24h"] = ("Last 24h", new() { { "fr", "24 dernières heures" }, { "en", "Last 24h" } }),
            ["admin.print.last_7d"] = ("Last 7 days", new() { { "fr", "7 derniers jours" }, { "en", "Last 7 days" } }),
            ["admin.print.last_30d"] = ("Last 30 days", new() { { "fr", "30 derniers jours" }, { "en", "Last 30 days" } }),
            ["admin.db.section_title"] = ("Database", new() { { "fr", "Base de données" }, { "en", "Database" } }),
            ["admin.stripe.section_title"] = ("Stripe", new() { { "fr", "Stripe" }, { "en", "Stripe" } }),
            ["admin.stripe.not_tested_or_failed"] = ("Stripe status unavailable", new() { { "fr", "Statut Stripe indisponible" }, { "en", "Stripe status unavailable" } }),
            ["admin.stripe.mode_label"] = ("Mode", new() { { "fr", "Mode" }, { "en", "Mode" } }),
            ["admin.stripe.mode_test"] = ("Test", new() { { "fr", "Test" }, { "en", "Test" } }),
            ["admin.stripe.mode_live"] = ("Live", new() { { "fr", "Production" }, { "en", "Live" } }),
            ["admin.stripe.webhook_secret_label"] = ("Webhook secret configured", new() { { "fr", "Secret webhook configuré" }, { "en", "Webhook secret configured" } }),
            ["admin.stripe.test_button"] = ("Test Stripe", new() { { "fr", "Tester Stripe" }, { "en", "Test Stripe" } }),
            ["admin.stripe.success.connected"] = ("Stripe connection verified", new() { { "fr", "Connexion Stripe vérifiée" }, { "en", "Stripe connection verified" } }),
            ["admin.smtp.section_title"] = ("SMTP", new() { { "fr", "SMTP" }, { "en", "SMTP" } }),
            ["admin.smtp.target_email_label"] = ("Recipient email", new() { { "fr", "Adresse e-mail destinataire" }, { "en", "Recipient email" } }),
            ["admin.smtp.target_email_placeholder"] = ("admin@example.com", new() { { "fr", "admin@exemple.fr" }, { "en", "admin@example.com" } }),
            ["admin.smtp.send_button"] = ("Send test email", new() { { "fr", "Envoyer un e-mail de test" }, { "en", "Send test email" } }),
            ["admin.smtp.last_latency"] = ("Last send duration", new() { { "fr", "Dernière durée d'envoi" }, { "en", "Last send duration" } }),
            ["admin.smtp.success.sent"] = ("Test email sent", new() { { "fr", "E-mail de test envoyé" }, { "en", "Test email sent" } }),
            ["admin.smtp.test_connection_button"] = ("Test SMTP connection", new() { { "fr", "Tester la connexion SMTP" }, { "en", "Test SMTP connection" } }),
            ["admin.smtp.success.connected"] = ("SMTP connection verified", new() { { "fr", "Connexion SMTP vérifiée" }, { "en", "SMTP connection verified" } }),
            ["admin.smtp.error.connection_failed"] = ("SMTP connection failed", new() { { "fr", "Échec de connexion SMTP" }, { "en", "SMTP connection failed" } }),

            ["app.error.unknown"] = ("An unexpected error occurred.", new() { { "fr", "Une erreur inattendue s'est produite." }, { "en", "An unexpected error occurred." } }),
            ["app.success.saved"] = ("Saved successfully.", new() { { "fr", "Enregistré avec succès." }, { "en", "Saved successfully." } }),
            ["app.success.deleted"] = ("Deleted successfully.", new() { { "fr", "Supprimé avec succès." }, { "en", "Deleted successfully." } }),
            ["app.success.login"] = ("Login successful.", new() { { "fr", "Connexion réussie." }, { "en", "Login successful." } }),

            ["common.back"] = ("Back", new() { { "fr", "Retour" }, { "en", "Back" } }),
            ["common.no_records"] = ("No records found.", new() { { "fr", "Aucun enregistrement." }, { "en", "No records found." } }),
            ["common.form"] = ("Form", new() { { "fr", "Formulaire" }, { "en", "Form" } }),
            ["common.close"] = ("Close", new() { { "fr", "Fermer" }, { "en", "Close" } }),
            ["common.add"] = ("Add", new() { { "fr", "Ajouter" }, { "en", "Add" } }),
            ["common.edit"] = ("Edit", new() { { "fr", "Modifier" }, { "en", "Edit" } }),
            ["common.delete"] = ("Delete", new() { { "fr", "Supprimer" }, { "en", "Delete" } }),
            ["common.save"] = ("Save", new() { { "fr", "Enregistrer" }, { "en", "Save" } }),
            ["common.cancel"] = ("Cancel", new() { { "fr", "Annuler" }, { "en", "Cancel" } }),
            ["common.loading"] = ("Loading...", new() { { "fr", "Chargement en cours..." }, { "en", "Loading..." } }),
            ["common.currency"] = ("€", new() { { "fr", "€" }, { "en", "€" } }),
            ["common.yes"] = ("Yes", new() { { "fr", "Oui" }, { "en", "Yes" } }),
            ["common.no"] = ("No", new() { { "fr", "Non" }, { "en", "No" } }),
            ["common.filter_placeholder"] = ("Filter...", new() { { "fr", "Filtrer..." }, { "en", "Filter..." } }),
            ["common.user"] = ("User", new() { { "fr", "Utilisateur" }, { "en", "User" } }),
            ["common.reseller"] = ("Reseller", new() { { "fr", "Revendeur" }, { "en", "Reseller" } }),
            ["common.created_at"] = ("Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } }),
            ["common.name"] = ("Name", new() { { "fr", "Nom" }, { "en", "Name" } }),
            ["common.status"] = ("Status", new() { { "fr", "Statut" }, { "en", "Status" } }),
            ["common.updated_at"] = ("Updated At", new() { { "fr", "Mis à jour le" }, { "en", "Updated At" } }),
            ["common.password"] = ("Password", new() { { "fr", "Mot de passe" }, { "en", "Password" } }),
            ["common.success"] = ("Success", new() { { "fr", "Succès" }, { "en", "Success" } }),
            ["common.error"] = ("Error", new() { { "fr", "Erreur" }, { "en", "Error" } }),
            ["common.email"] = ("Email", new() { { "fr", "E-mail" }, { "en", "Email" } }),
            ["common.role"] = ("Role", new() { { "fr", "Rôle" }, { "en", "Role" } }),
            ["common.monthly"] = ("Monthly", new() { { "fr", "Mensuel" }, { "en", "Monthly" } }),
            ["common.yearly"] = ("Yearly", new() { { "fr", "Annuel" }, { "en", "Yearly" } }),
            
            ["clients.title"] = ("Clients", new() { { "fr", "Clients" }, { "en", "Clients" } }),
            ["clients.description"] = ("Manage your end-clients and track your associated licenses.", new() { { "fr", "Gérez vos clients finaux et suivez vos licences associées." }, { "en", "Manage your end-clients and track your associated licenses." } }),
            ["clients.no_clients"] = ("No clients found.", new() { { "fr", "Aucun client trouvé." }, { "en", "No clients found." } }),
            ["clients.add_title"] = ("Add Client", new() { { "fr", "Ajouter un client" }, { "en", "Add Client" } }),
            ["clients.edit_title"] = ("Edit Client", new() { { "fr", "Modifier le client" }, { "en", "Edit Client" } }),

            ["printjobs.title"] = ("Print Jobs", new() { { "fr", "Travaux d'impression" }, { "en", "Print Jobs" } }),
            ["printjobs.description"] = ("View your print jobs.", new() { { "fr", "Consultez vos travaux d'impression." }, { "en", "View your print jobs." } }),
            ["printjobs.no_printjobs"] = ("No print jobs available.", new() { { "fr", "Aucun travail d'impression disponible." }, { "en", "No print jobs available." } }),
            ["printjobs.printer"] = ("Printer", new() { { "fr", "Imprimante" }, { "en", "Printer" } }),
            ["printjobs.template"] = ("Template", new() { { "fr", "Modèle" }, { "en", "Template" } }),
            ["printjobs.quantity"] = ("Quantity", new() { { "fr", "Quantité" }, { "en", "Quantity" } }),
            ["printjobs.date"] = ("Date", new() { { "fr", "Date" }, { "en", "Date" } }),
            ["printjobs.response_message"] = ("Response Message", new() { { "fr", "Message de réponse" }, { "en", "Response Message" } }),
            ["printjobs.add_title"] = ("Add Print Job", new() { { "fr", "Ajouter un travail d'impression" }, { "en", "Add Print Job" } }),
            ["printjobs.edit_title"] = ("Edit Print Job", new() { { "fr", "Modifier le travail d'impression" }, { "en", "Edit Print Job" } }),

            ["users.role_reseller"] = ("Reseller", new() { { "fr", "Revendeur" }, { "en", "Reseller" } }),
            ["users.role_admin"] = ("Admin", new() { { "fr", "Admin" }, { "en", "Admin" } }),
            ["users.role_user"] = ("User", new() { { "fr", "Utilisateur" }, { "en", "User" } }),

            ["users.title"] = ("Users", new() { { "fr", "Utilisateurs" }, { "en", "Users" } }),            
            ["users.password_help"] = ("Leave blank to keep the current password when editing.", new() { { "fr", "Laisser vide pour conserver le mot de passe actuel lors d'une modification." }, { "en", "Leave blank to keep the current password when editing." } }),
            ["users.description"] = ("Manage your application users.", new() { { "fr", "Gérez vos utilisateurs." }, { "en", "Manage your application users." } }),
            ["users.no_users"] = ("No users found.", new() { { "fr", "Aucun utilisateur trouvé." }, { "en", "No users found." } }),
            ["users.is_admin"] = ("Administrator", new() { { "fr", "Administrateur" }, { "en", "Administrator" } }),
            ["users.is_reseller"] = ("Reseller", new() { { "fr", "Revendeur" }, { "en", "Reseller" } }),
            ["users.add_title"] = ("Add User", new() { { "fr", "Ajouter un utilisateur" }, { "en", "Add User" } }),
            ["users.edit_title"] = ("Edit User", new() { { "fr", "Modifier l'utilisateur" }, { "en", "Edit User" } }),

            ["agents.error.type_required"] = ("Please select an agent type", new() { { "fr", "Veuillez sélectionner un type d'agent" }, { "en", "Please select an agent type" } }),
            ["agents.api_key_readonly_help"] = ("This key is automatically generated and cannot be modified.", new() { { "fr", "Cette clé est générée automatiquement et ne peut pas être modifiée." }, { "en", "This key is automatically generated and cannot be modified." } }),
            ["agents.select_type"] = ("-- Select a type --", new() { { "fr", "-- Sélectionner un type --" }, { "en", "-- Select a type --" } }),
            ["agents.title"] = ("Agents", new() { { "fr", "Agents" }, { "en", "Agents" } }),
            ["agents.description"] = ("View your printing agents.", new() { { "fr", "Consultez vos agents d'impression." }, { "en", "View your printing agents." } }),
            ["agents.no_agents"] = ("No active agents at the moment.", new() { { "fr", "Aucun agent actif pour le moment." }, { "en", "No active agents at the moment." } }),
            ["agents.type"] = ("Type", new() { { "fr", "Type" }, { "en", "Type" } }),
            ["agents.api_key"] = ("API Key", new() { { "fr", "Clé API" }, { "en", "API Key" } }),
            ["agents.last_seen"] = ("Last Seen", new() { { "fr", "Dernière vue" }, { "en", "Last Seen" } }),
            ["agents.add_title"] = ("Add Agent", new() { { "fr", "Ajouter un agent" }, { "en", "Add Agent" } }),
            ["agents.edit_title"] = ("Edit Agent", new() { { "fr", "Modifier l'agent" }, { "en", "Edit Agent" } }),
            ["agents.copy_api_key"] = ("Copy API key", new() { { "fr", "Copier la clé API" }, { "en", "Copy API key" } }),
            ["agents.success.api_key_copied"] = ("API key copied to clipboard", new() { { "fr", "Clé API copiée dans le presse-papiers" }, { "en", "API key copied to clipboard" } }),
            ["agents.error.copy_api_key_failed"] = ("Unable to copy API key", new() { { "fr", "Impossible de copier la clé API" }, { "en", "Unable to copy API key" } }),

            ["printers.activation.name"] = ("My first printer", new() { { "fr", "Ma première imprimante" }, { "en", "My first printer" } }),
            ["printers.select_protocol"] = ("Select a protocol...", new() { { "fr", "Sélectionner un protocole..." }, { "en", "Select a protocol..." } }),
            ["printers.select_resolution"] = ("Select a resolution...", new() { { "fr", "Sélectionner une résolution..." }, { "en", "Select a resolution..." } }),
            ["printers.protocol"] = ("Protocol", new() { { "fr", "Protocole" }, { "en", "Protocol" } }),
            ["printers.resolution"] = ("Resolution", new() { { "fr", "Résolution" }, { "en", "Resolution" } }),
            ["printers.title"] = ("Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } }),
            ["printers.description"] = ("Manage your network printers.", new() { { "fr", "Gérez vos imprimantes réseau." }, { "en", "Manage your network printers." } }),
            ["printers.no_printers"] = ("No printers configured.", new() { { "fr", "Aucune imprimante configurée." }, { "en", "No printers configured." } }),
            ["printers.add_title"] = ("Add Printer", new() { { "fr", "Ajouter une imprimante" }, { "en", "Add Printer" } }),
            ["printers.edit_title"] = ("Edit Printer", new() { { "fr", "Modifier l'imprimante" }, { "en", "Edit Printer" } }),
            ["printers.location"] = ("Location", new() { { "fr", "Emplacement" }, { "en", "Location" } }),
            ["printers.ip_port"] = ("IP : Port", new() { { "fr", "IP : Port" }, { "en", "IP : Port" } }),
            ["printers.ip"] = ("IP Address", new() { { "fr", "Adresse IP" }, { "en", "IP Address" } }),
            ["printers.port"] = ("Port", new() { { "fr", "Port" }, { "en", "Port" } }),

            ["template.activation.name"] = ("My first label", new() { { "fr", "Ma première étiquette" }, { "en", "My first label" } }),
            ["templates.render.title"] = ("Label Renderer Editor", new() { { "fr", "Éditeur de rendu d'étiquette" }, { "en", "Label Renderer Editor" } }),
            ["templates.render.preview"] = ("Preview", new() { { "fr", "Prévisualisation" }, { "en", "Preview" } }),
            ["templates.edit_renderer"] = ("Edit Label Renderer", new() { { "fr", "Éditer le rendu de l'étiquette" }, { "en", "Edit Label Renderer" } }),
            ["templates.title"] = ("Label Templates", new() { { "fr", "Gestion des modèles" }, { "en", "Label Templates" } }),
            ["templates.description"] = ("Manage your label templates.", new() { { "fr", "Gérez vos modèles d'étiquettes." }, { "en", "Manage your label templates." } }),
            ["templates.no_templates"] = ("No templates available.", new() { { "fr", "Aucun modèle disponible." }, { "en", "No templates available." } }),
            ["templates.add_title"] = ("Add Template", new() { { "fr", "Ajouter un modèle" }, { "en", "Add Template" } }),
            ["templates.edit_title"] = ("Edit Template", new() { { "fr", "Modifier le modèle" }, { "en", "Edit Template" } }),

            ["nav.clients"] = ("My clients", new() { { "fr", "Mes clients" }, { "en", "My clients" } }),
            ["nav.home"] = ("Home", new() { { "fr", "Accueil" }, { "en", "Home" } }),
            ["nav.templates"] = ("Label Templates", new() { { "fr", "Modèles d'étiquettes" }, { "en", "Label Templates" } }),
            ["nav.printers"] = ("Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } }),
            ["nav.agents"] = ("Agents", new() { { "fr", "Agents" }, { "en", "Agents" } }),
            ["nav.users"] = ("Users", new() { { "fr", "Utilisateurs" }, { "en", "Users" } }),
            ["nav.printjobs"] = ("Print Jobs", new() { { "fr", "Tâches d'impression" }, { "en", "Print Jobs" } }),
            ["nav.my_account"] = ("My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } }),
            ["nav.logout"] = ("Logout", new() { { "fr", "Déconnexion" }, { "en", "Logout" } }),
            ["nav.admin_dashboard"] = ("Dashboard", new() { { "fr", "Tableau de bord" }, { "en", "Dashboard" } }),

            ["account.language_label"] = ("Language", new() { { "fr", "Langue" }, { "en", "Language" } }),
            ["account.email_change_warning"] = ("Changing your email will require you to reactivate your account.", new() { { "fr", "Modifier votre e-mail nécessitera une réactivation de votre compte via le lien reçu." }, { "en", "Changing your email will require you to reactivate your account." } }),
            ["account.password_section"] = ("Change Password", new() { { "fr", "Modifier le mot de passe" }, { "en", "Change Password" } }),
            ["account.new_password_label"] = ("New Password", new() { { "fr", "Nouveau mot de passe" }, { "en", "New Password" } }),
            ["account.password_placeholder_leave_blank"] = ("Leave blank to keep current password", new() { { "fr", "Laisser vide pour conserver le mot de passe actuel" }, { "en", "Leave blank to keep current password" } }),
            ["account.success.updated"] = ("Account updated successfully", new() { { "fr", "Compte mis à jour avec succès" }, { "en", "Account updated successfully" } }),
            ["account.warning.email_changed_inactive"] = ("Your email has changed. Please check your inbox to activate your account.", new() { { "fr", "Votre e-mail a changé. Veuillez vérifier votre boîte de réception pour réactiver votre compte." }, { "en", "Your email has changed. Please check your inbox to activate your account." } }),
            ["account.title"] = ("My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } }),
            ["account.profile_section"] = ("Profile Information", new() { { "fr", "Informations du profil" }, { "en", "Profile Information" } }),
            ["account.subscriptions_title"] = ("My Subscriptions", new() { { "fr", "Mes abonnements" }, { "en", "My Subscriptions" } }),
            ["account.no_subscriptions"] = ("No subscriptions found.", new() { { "fr", "Aucun abonnement trouvé." }, { "en", "No subscriptions found." } }),
            ["account.subscription.plan"] = ("Plan", new() { { "fr", "Plan" }, { "en", "Plan" } }),
            ["account.subscription.currentperiodstart"] = ("Last Period Start", new() { { "fr", "Début Dernière Période" }, { "en", "Last Period Start" } }),
            ["account.subscription.currentperiodend"] = ("Last Period End", new() { { "fr", "Fin Dernière Période" }, { "en", "Last Period End" } }),
            ["account.subscription.period"] = ("Period", new() { { "fr", "Période" }, { "en", "Period" } }),
            ["account.current_subscription_title"] = ("Current subscription", new() { { "fr", "Abonnement actuel" }, { "en", "Current subscription" } }),
            ["account.no_active_subscription"] = ("No active subscription", new() { { "fr", "Aucun abonnement actif" }, { "en", "No active subscription" } }),
            ["account.subscription.billing_cycle"] = ("Billing cycle", new() { { "fr", "Cycle de facturation" }, { "en", "Billing cycle" } }),
            ["account.subscription.period_dates"] = ("Current period", new() { { "fr", "Période en cours" }, { "en", "Current period" } }),
            ["account.subscription.period_free"] = ("Free", new() { { "fr", "Gratuit" }, { "en", "Free" } }),
            ["account.subscription.months"] = ("months", new() { { "fr", "mois" }, { "en", "months" } }),
            ["account.subscription.monthly_usage"] = ("Prints used this month", new() { { "fr", "Impressions utilisées ce mois-ci" }, { "en", "Prints used this month" } }),
            ["account.subscription.extra_prints"] = ("Remaining top-up credits", new() { { "fr", "Crédits de recharge restants" }, { "en", "Remaining top-up credits" } }),
            ["account.subscription.total_available"] = ("Total available", new() { { "fr", "Total disponible" }, { "en", "Total available" } }),
            ["account.subscription.change_plan_button"] = ("Change plan", new() { { "fr", "Changer d'offre" }, { "en", "Change plan" } }),

            ["subscription.modal.subscribed_success"] = ("Subscription updated.", new() { { "fr", "Offre mise à jour." }, { "en", "Subscription updated." } }),
            ["subscription.modal.title"] = ("Choose a plan", new() { { "fr", "Choisir une offre" }, { "en", "Choose a plan" } }),
            ["subscription.modal.subtitle"] = ("Select the plan and billing cycle that fit your printing needs.", new() { { "fr", "Sélectionnez l'offre et le cycle de facturation adaptés à vos besoins." }, { "en", "Select the plan and billing cycle that fit your printing needs." } }),
            ["subscription.modal.billing_cycle"] = ("Billing cycle", new() { { "fr", "Cycle de facturation" }, { "en", "Billing cycle" } }),
            ["subscription.modal.no_plans"] = ("No plans available at the moment.", new() { { "fr", "Aucune offre disponible pour le moment." }, { "en", "No plans available at the moment." } }),
            ["subscription.modal.current_plan"] = ("Current plan", new() { { "fr", "Offre actuelle" }, { "en", "Current plan" } }),
            ["subscription.modal.free_price"] = ("Free", new() { { "fr", "Gratuit" }, { "en", "Free" } }),
            ["subscription.modal.per_month"] = ("/ month", new() { { "fr", "/ mois" }, { "en", "/ month" } }),
            ["subscription.modal.per_year"] = ("/ year", new() { { "fr", "/ an" }, { "en", "/ year" } }),
            ["subscription.modal.prints_included"] = ("prints included / month", new() { { "fr", "impressions incluses / mois" }, { "en", "prints included / month" } }),
            ["subscription.modal.topup_info"] = ("Top-up available", new() { { "fr", "Recharge disponible" }, { "en", "Top-up available" } }),
            ["subscription.modal.current_plan_button"] = ("Active plan", new() { { "fr", "Offre active" }, { "en", "Active plan" } }),
            ["subscription.modal.select_free"] = ("Select free plan", new() { { "fr", "Choisir l'offre gratuite" }, { "en", "Select free plan" } }),
            ["subscription.modal.subscribe_button"] = ("Subscribe", new() { { "fr", "Souscrire" }, { "en", "Subscribe" } }),

            ["home.subscription.period_free"] = ("Free plan", new() { { "fr", "Offre gratuite" }, { "en", "Free plan" } }),
            ["home.subscription.monthly_usage"] = ("Prints used this month", new() { { "fr", "Impressions utilisées ce mois-ci" }, { "en", "Prints used this month" } }),
            ["home.subscription.extra_prints"] = ("Top-up balance", new() { { "fr", "Solde recharge" }, { "en", "Top-up balance" } }),
            ["home.subscription.total_available"] = ("Available", new() { { "fr", "Disponible" }, { "en", "Available" } }),
            ["home.subscription.topup_button"] = ("Top up prints", new() { { "fr", "Recharger" }, { "en", "Top up prints" } }),
            ["home.subscription.checkout_success"] = ("Your subscription has been successfully activated!", new() { { "fr", "Votre abonnement a été activé avec succès !" }, { "en", "Your subscription has been successfully activated!" } }),
            ["home.subscription.checkout_cancel"] = ("The payment process was canceled.", new() { { "fr", "Le processus de paiement a été annulé." }, { "en", "The payment process was canceled." } }),
            ["home.title"] = ("Home", new() { { "fr", "Accueil" }, { "en", "Home" } }),
            ["home.welcome_subtitle"] = ("Welcome to your FastLabel management area.", new() { { "fr", "Bienvenue sur votre espace de gestion FastLabel." }, { "en", "Welcome to your FastLabel management area." } }),
            ["home.profile_status"] = ("Profile Status", new() { { "fr", "Statut du profil" }, { "en", "Profile Status" } }),
            ["home.current_subscription"] = ("Current Subscription", new() { { "fr", "Abonnement actuel" }, { "en", "Current Subscription" } }),
            ["home.security"] = ("Security & Account", new() { { "fr", "Sécurité & Compte" }, { "en", "Security & Account" } }),
            ["home.subscription.active"] = ("Active Subscription", new() { { "fr", "Abonnement Actif" }, { "en", "Active Subscription" } }),
            ["home.subscription.none"] = ("No Subscription", new() { { "fr", "Aucun abonnement" }, { "en", "No Subscription" } }),
            ["home.subscription.period_prefix"] = ("Period:", new() { { "fr", "Période :" }, { "en", "Period:" } }),
            ["home.subscription.period_suffix"] = ("months (Stripe Linked)", new() { { "fr", "mois (Lié Stripe)" }, { "en", "months (Stripe Linked)" } }),
            ["home.subscription.subscribe"] = ("Subscribe to a plan", new() { { "fr", "Souscrire à une offre" }, { "en", "Subscribe to a plan" } }),
            ["home.security_title"] = ("Security & Profile", new() { { "fr", "Sécurité & Profil" }, { "en", "Security & Profile" } }),
            ["home.manage_account"] = ("Manage account", new() { { "fr", "Gérer mon compte" }, { "en", "Manage account" } }),
            ["home.field.member_since"] = ("Member since", new() { { "fr", "Membre depuis" }, { "en", "Member since" } }),

            ["error.not_found_title"] = ("Page not found", new() { { "fr", "Page non trouvée" }, { "en", "Page not found" } }),
            ["error.not_found_message"] = ("Sorry, the page you are looking for does not exist.", new() { { "fr", "Désolé, la page que vous recherchez n'existe pas." }, { "en", "Sorry, the page you are looking for does not exist." } }),

            ["auth.submitting"] = ("Submitting...", new() { { "fr", "En cours..." }, { "en", "Submitting..." } }),
            ["auth.processing"] = ("Processing...", new() { { "fr", "Traitement en cours..." }, { "en", "Processing..." } }),
            ["auth.success.email_sent"] = ("Recovery email sent successfully.", new() { { "fr", "Email de récupération envoyé avec succès." }, { "en", "Recovery email sent successfully." } }),
            ["auth.error.general"] = ("An error occurred.", new() { { "fr", "Une erreur est survenue." }, { "en", "An error occurred." } }),
            ["auth.error.unauthorized_title"] = ("Unauthorized access", new() { { "fr", "Accès non autorisé" }, { "en", "Unauthorized access" } }),
            ["auth.error.unauthorized_message"] = ("You must log in to access this page.", new() { { "fr", "Vous devez vous connecter pour accéder à cette page." }, { "en", "You must log in to access this page." } }),
            ["auth.error.invalid_or_expired_token"] = ("Invalid or expired reset token.", new() { { "fr", "Jeton de réinitialisation invalide ou expiré." }, { "en", "Invalid or expired reset token." } }),
            ["auth.reset_password_title"] = ("Reset your password", new() { { "fr", "Réinitialiser votre mot de passe" }, { "en", "Reset your password" } }),
            ["auth.new_password"] = ("New password", new() { { "fr", "Nouveau mot de passe" }, { "en", "New password" } }),
            ["auth.success.password_reset"] = ("Your password has been successfully reset. You can now log in.", new() { { "fr", "Votre mot de passe a été réinitialisé avec succès. Vous pouvez maintenant vous connecter." }, { "en", "Your password has been successfully reset. You can now log in." } }),
            ["auth.error.missing_token"] = ("Invalid or missing reset token.", new() { { "fr", "Jeton de réinitialisation invalide ou manquant." }, { "en", "Invalid or missing reset token." } }),
            ["auth.success.account_activated"] = ("Your account has been successfully activated. You can now log in.", new() { { "fr", "Votre compte a été activé avec succès. Vous pouvez maintenant vous connecter." }, { "en", "Your account has been successfully activated. You can now log in." } }),
            ["auth.error.email_already_taken"] = ("This email is already taken.", new() { { "fr", "Cet email est déjà utilisé." }, { "en", "This email is already taken." } }),
            ["auth.success.registered"] = ("Registration successful.", new() { { "fr", "Inscription réussie." }, { "en", "Registration successful." } }),
            ["auth.name"] = ("Full Name", new() { { "fr", "Nom complet" }, { "en", "Full Name" } }),
            ["auth.name_placeholder"] = ("John Doe", new() { { "fr", "Jean Dupont" }, { "en", "John Doe" } }),
            ["auth.error.invalid_credentials"] = ("Invalid credentials.", new() { { "fr", "Identifiants invalides." }, { "en", "Invalid credentials." } }),
            ["auth.error.server_unreachable"] = ("Unable to reach the server.", new() { { "fr", "Impossible de joindre le serveur." }, { "en", "Unable to reach the server." } }),
            ["auth.sending"] = ("Sending...", new() { { "fr", "Envoi en cours..." }, { "en", "Sending..." } }),
            ["auth.forgot_success_message"] = ("If an account matches this email, a recovery link has been sent.", new() { { "fr", "Si un compte est associé à cet email, un lien de réinitialisation a été envoyé." }, { "en", "If an account matches this email, a recovery link has been sent." } }),
            ["auth.error.request_failed"] = ("An error occurred while processing your request.", new() { { "fr", "Une erreur est survenue lors de la demande." }, { "en", "An error occurred while processing your request." } }),
            ["auth.error.registration_failed"] = ("An error occurred during registration.", new() { { "fr", "Erreur lors de l'inscription." }, { "en", "An error occurred during registration." } }),
            ["auth.login_title"] = ("Login", new() { { "fr", "Connexion" }, { "en", "Login" } }),
            ["auth.submit"] = ("Sign In", new() { { "fr", "Se connecter" }, { "en", "Sign In" } }),
            ["auth.create_account"] = ("Create an account", new() { { "fr", "Créer un compte" }, { "en", "Create an account" } }),
            ["auth.forgot_password"] = ("Forgot password?", new() { { "fr", "Mot de passe oublié ?" }, { "en", "Forgot password?" } }),
            ["auth.register_title"] = ("Register", new() { { "fr", "Inscription" }, { "en", "Register" } }),
            ["auth.register_button"] = ("Sign Up", new() { { "fr", "S'inscrire" }, { "en", "Sign Up" } }),
            ["auth.already_have_account"] = ("Already have an account? Sign in", new() { { "fr", "Déjà un compte ? Connectez-vous" }, { "en", "Already have an account? Sign in" } }),
            ["auth.forgot_heading"] = ("Forgot password", new() { { "fr", "Mot de passe oublié" }, { "en", "Forgot password" } }),
            ["auth.forgot_instruction"] = ("Enter your email address to receive password reset instructions.", new() { { "fr", "Entre ton adresse email pour recevoir les instructions de réinitialisation." }, { "en", "Enter your email address to receive password reset instructions." } }),
            ["auth.email_placeholder"] = ("name@example.com", new() { { "fr", "nom@exemple.com" }, { "en", "name@example.com" } }),
            ["auth.send_link_button"] = ("Send Link", new() { { "fr", "Envoyer le lien" }, { "en", "Send Link" } }),
            ["auth.back_to_login"] = ("Back to login", new() { { "fr", "Retour à la connexion" }, { "en", "Back to login" } }),
            ["auth.email_placeholder_alt"] = ("your@email.com", new() { { "fr", "votre@email.com" }, { "en", "your@email.com" } }),

            // 2. Emails
            ["email.activation.subject"] = ("Activate your FastLabel account", new() { { "fr", "Activez votre compte FastLabel" }, { "en", "Activate your FastLabel account" } }),
            ["email.activation.body"] = ("Hello {0},\n\nPlease click the following link to activate your account:\n{1}", new() { { "fr", "Bonjour {0},\n\nVeuillez cliquer sur le lien suivant pour activer votre compte :\n{1}" }, { "en", "Hello {0},\n\nPlease click the following link to activate your account:\n{1}" } }),
            ["email.reset.subject"] = ("Reset your FastLabel password", new() { { "fr", "Réinitialisez votre mot de passe FastLabel" }, { "en", "Reset your FastLabel password" } }),
            ["email.reset.body"] = ("Hello {0},\n\nPlease click the following link to reset your password:\n{1}", new() { { "fr", "Bonjour {0},\n\nVeuillez cliquer sur le lien suivant pour réinitialiser votre mot de passe :\n{1}" }, { "en", "Hello {0},\n\nPlease click the following link to reset your password:\n{1}" } }),

            // 3. DTOs Restrictions
            ["language.error.code_required"] = ("Language code is required", new() { { "fr", "Le code de la langue est requis" }, { "en", "Language code is required" } }),
            ["language.error.code_max_length"] = ("Language code cannot exceed 10 characters", new() { { "fr", "Le code de la langue ne peut pas dépasser 10 caractères" }, { "en", "Language code cannot exceed 10 characters" } }),
            ["language.error.description_required"] = ("Language description is required", new() { { "fr", "La description de la langue est requise" }, { "en", "Language description is required" } }),

            ["stripe.error.plan_required"] = ("The plan is required.", new() { { "fr", "Le plan est obligatoire." }, { "en", "The plan is required." } }),
            ["stripe.error.plan_max_length"] = ("The plan cannot exceed 50 characters.", new() { { "fr", "Le plan ne peut pas dépasser 50 caractères." }, { "en", "The plan cannot exceed 50 characters." } }),
            ["stripe.error.interval_required"] = ("The billing interval is required.", new() { { "fr", "L'intervalle de facturation est obligatoire." }, { "en", "The billing interval is required." } }),
            ["stripe.error.interval_max_length"] = ("The interval cannot exceed 20 characters.", new() { { "fr", "L'intervalle ne peut pas dépasser 20 caractères." }, { "en", "The interval cannot exceed 20 characters." } }),

            ["subscription.error.plan_required"] = ("Plan name is required", new() { { "fr", "Le nom du plan est requis" }, { "en", "Plan name is required" } }),
            ["subscription.error.period_invalid"] = ("Invalid subscription period", new() { { "fr", "Période d'abonnement invalide" }, { "en", "Invalid subscription period" } }),

            ["resolutions.error.value_required"] = ("Resolution value is required", new() { { "fr", "La valeur de la résolution est requise" }, { "en", "Resolution value is required" } }),
            ["resolutions.error.value_range"] = ("Resolution value must be greater than 0", new() { { "fr", "La valeur de la résolution doit être supérieure à 0" }, { "en", "Resolution value must be greater than 0" } }),

            ["protocols.error.name_required"] = ("Protocol name is required", new() { { "fr", "Le nom du protocole est requis" }, { "en", "Protocol name is required" } }),

            ["agent.error.name_required"] = ("Agent name is required", new() { { "fr", "Le nom de l'agent est requis" }, { "en", "Agent name is required" } }),
            ["agent.error.name_max_length"] = ("Agent name cannot exceed 100 characters", new() { { "fr", "Le nom de l'agent ne peut pas dépasser 100 caractères" }, { "en", "Agent name cannot exceed 100 characters" } }),
            ["agent.error.type_required"] = ("Agent type is required", new() { { "fr", "Le type d'agent est requis" }, { "en", "Agent type is required" } }),
            ["agent.error.apikey_required"] = ("API key is required", new() { { "fr", "La clé d'API est requise" }, { "en", "API key is required" } }),

            ["printjobs.error.printer_name_required"] = ("Printer name is required", new() { { "fr", "Le nom de l'imprimante est requis" }, { "en", "Printer name is required" } }),
            ["printjobs.error.template_name_required"] = ("Template name is required", new() { { "fr", "Le nom du modèle est requis" }, { "en", "Template name is required" } }),
            ["printjobs.error.input_data_required"] = ("Input data JSON is required", new() { { "fr", "Les données d'entrée JSON sont requises" }, { "en", "Input data JSON is required" } }),
            ["printjobs.error.quantity_range"] = ("Quantity must be between 1 and 10000", new() { { "fr", "La quantité doit être comprise entre 1 et 10000" }, { "en", "Quantity must be between 1 and 10000" } }),

            ["print.error.apikey_required"] = ("API key is required", new() { { "fr", "La clé d'API est requise" }, { "en", "API key is required" } }),
            ["print.error.printer_id_required"] = ("Printer ID is required", new() { { "fr", "L'identifiant de l'imprimante est requis" }, { "en", "Printer ID is required" } }),
            ["print.error.printer_id_invalid"] = ("PrinterId must be a valid positive number", new() { { "fr", "L'identifiant de l'imprimante doit être un nombre positif valide" }, { "en", "PrinterId must be a valid positive number" } }),
            ["print.error.quantity_required"] = ("Quantity is required", new() { { "fr", "La quantité est requise" }, { "en", "Quantity is required" } }),
            ["print.error.quantity_range"] = ("Quantity must be between 1 and 10000", new() { { "fr", "La quantité doit être comprise entre 1 et 10000" }, { "en", "Quantity must be between 1 and 10000" } }),

            ["auth.error.password_min_length"] = ("Password must be at least 8 characters long", new() { { "fr", "Le mot de passe doit contenir au moins 8 caractères" }, { "en", "Password must be at least 8 characters long" } }),

            ["templates.json_data"] = ("JSON Data", new() { { "fr", "Données JSON" }, { "en", "JSON Data" } }),

            ["translation.error.key_required"] = ("Translation key is required", new() { { "fr", "La clé de traduction est requise" }, { "en", "Translation key is required" } }),
            ["translation.error.value_required"] = ("Translation value is required", new() { { "fr", "La valeur de traduction est requise" }, { "en", "Translation value is required" } }),

            ["user.error.name_required"] = ("Name is required", new() { { "fr", "Le nom est requis" }, { "en", "Name is required" } }),
            ["user.error.name_max_length"] = ("Name cannot exceed 100 characters", new() { { "fr", "Le nom ne peut pas dépasser 100 caractères" }, { "en", "Name cannot exceed 100 characters" } }),
            ["user.error.language_required"] = ("Language is required", new() { { "fr", "La langue est requise" }, { "en", "Language is required" } }),

            ["auth.error.name_required"] = ("Name is required", new() { { "fr", "Le nom est requis" }, { "en", "Name is required" } }),
            ["auth.error.email_required"] = ("Email is required", new() { { "fr", "L'adresse email est requise" }, { "en", "Email is required" } }),
            ["auth.error.email_invalid"] = ("Invalid email address", new() { { "fr", "Adresse email invalide" }, { "en", "Invalid email address" } }),
            ["auth.error.password_required"] = ("Password is required", new() { { "fr", "Le mot de passe est requis" }, { "en", "Password is required" } }),
            ["auth.error.token_required"] = ("Token is required", new() { { "fr", "Le jeton est requis" }, { "en", "Token is required" } }),

            ["printer.error.protocol_required"] = ("Please select a protocol", new() { { "fr", "Veuillez sélectionner un protocole" }, { "en", "Please select a protocol" } }),
            ["printer.error.resolution_required"] = ("Please select a resolution", new() { { "fr", "Veuillez sélectionner une résolution" }, { "en", "Please select a resolution" } }),
            ["printer.error.name_required"] = ("Printer name is required", new() { { "fr", "Le nom de l'imprimante est requis" }, { "en", "Printer name is required" } }),
            ["printer.error.name_max_length"] = ("Printer name cannot exceed 100 characters", new() { { "fr", "Le nom de l'imprimante ne peut pas dépasser 100 caractères" }, { "en", "Printer name cannot exceed 100 characters" } }),
            ["printer.error.location_required"] = ("Location is required", new() { { "fr", "L'emplacement est requis" }, { "en", "Location is required" } }),
            ["printer.error.ip_required"] = ("IP address is required", new() { { "fr", "L'adresse IP est requise" }, { "en", "IP address is required" } }),
            ["printer.error.port_invalid"] = ("Port must be between 1 and 65535", new() { { "fr", "Le port doit être compris entre 1 et 65535" }, { "en", "Port must be between 1 and 65535" } }),

            ["label.error.name_required"] = ("Label name is required", new() { { "fr", "Le nom de l'étiquette est requis" }, { "en", "Label name is required" } }),
            ["label.error.name_max_length"] = ("Label name cannot exceed 150 characters", new() { { "fr", "Le nom de l'étiquette ne peut pas dépasser 150 caractères" }, { "en", "Label name cannot exceed 150 characters" } }),
            ["label.error.content_required"] = ("Label content is required", new() { { "fr", "Le contenu de l'étiquette est requis" }, { "en", "Label content is required" } }),
            ["label.error.description_max_length"] = ("Description cannot exceed 500 characters", new() { { "fr", "La description ne peut pas dépasser 500 caractères" }, { "en", "Description cannot exceed 500 characters" } }),

            // 4. Endpoints
            ["smtp.error.server_missing"] = ("SMTP server is missing", new() { { "fr", "Le serveur SMTP est manquant" }, { "en", "SMTP server is missing" } }),
            ["smtp.error.port_invalid"] = ("Invalid SMTP port", new() { { "fr", "Port SMTP invalide" }, { "en", "Invalid SMTP port" } }),
            ["smtp.error.sender_missing"] = ("SMTP sender email is missing", new() { { "fr", "L'adresse expéditeur SMTP est manquante" }, { "en", "SMTP sender email is missing" } }),
            ["smtp.error.username_missing"] = ("SMTP username is missing", new() { { "fr", "Le nom d'utilisateur SMTP est manquant" }, { "en", "SMTP username is missing" } }),

            ["admin.db.error.connection_failed"] = ("Database connection failed", new() { { "fr", "Échec de connexion à la base de données" }, { "en", "Database connection failed" } }),
            ["admin.stripe.error.key_missing"] = ("Stripe API key missing", new() { { "fr", "Clé API Stripe manquante" }, { "en", "Stripe API key missing" } }),
            ["admin.stripe.error.connection_failed"] = ("Stripe connection failed", new() { { "fr", "Échec de connexion à Stripe" }, { "en", "Stripe connection failed" } }),
            ["admin.smtp.test.subject"] = ("FastLabel SMTP Test", new() { { "fr", "Test d'envoi SMTP FastLabel" }, { "en", "FastLabel SMTP Test" } }),
            ["admin.smtp.test.body"] = ("This is a test email from FastLabel.", new() { { "fr", "Ceci est un e-mail de test envoyé depuis FastLabel." }, { "en", "This is a test email from FastLabel." } }),
            ["admin.smtp.error.send_failed"] = ("Failed to send test email", new() { { "fr", "Échec de l'envoi de l'e-mail de test" }, { "en", "Failed to send test email" } }),
            ["admin.smtp.error.email_required"] = ("Email is required", new() { { "fr", "L'adresse e-mail est obligatoire" }, { "en", "Email is required" } }),
            ["admin.smtp.error.email_invalid"] = ("Invalid email address", new() { { "fr", "Adresse e-mail invalide" }, { "en", "Invalid email address" } }),

            ["common.error.no_ids_selected"] = ("Please select at least one item.", new() { { "fr", "Veuillez sélectionner au moins un élément." }, { "en", "Please select at least one item." } }),

            ["stripe.webhook.error.invalid_signature"] = ("Invalid Stripe webhook signature.", new() { { "fr", "Signature du webhook Stripe invalide." }, { "en", "Invalid Stripe webhook signature." } }),
            ["stripe.error.invalid_interval"] = ("Invalid billing interval.", new() { { "fr", "Intervalle de facturation invalide." }, { "en", "Invalid billing interval." } }),
            ["stripe.error.plan_not_found"] = ("Plan not found.", new() { { "fr", "Offre introuvable." }, { "en", "Plan not found." } }),
            ["stripe.error.price_not_configured"] = ("Stripe price is not configured for this plan.", new() { { "fr", "Le tarif Stripe n'est pas configuré pour cette offre." }, { "en", "Stripe price is not configured for this plan." } }),
            ["stripe.error.checkout_failed"] = ("Unable to create Stripe checkout session.", new() { { "fr", "Impossible de créer la session de paiement Stripe." }, { "en", "Unable to create Stripe checkout session." } }),
            ["stripe.webhook.error.missing_signature_or_secret"] = ("Missing Stripe signature or webhook secret", new() { { "fr", "Signature Stripe ou secret de webhook manquant" }, { "en", "Missing Stripe signature or webhook secret" } }),
            ["stripe.webhook.error.invalid_plan_upgrade"] = ("Invalid subscription plan upgrade", new() { { "fr", "Changement d'offre ou de durée d'abonnement non autorisé" }, { "en", "Invalid subscription plan upgrade" } }),
            ["stripe.error.api_key_missing"] = ("Stripe API key is missing.", new() { { "fr", "La clé API Stripe est manquante." }, { "en", "Stripe API key is missing." } }),
            ["stripe.error.subscription_item_not_found"] = ("No Stripe subscription item found.", new() { { "fr", "Aucun élément d'abonnement Stripe trouvé." }, { "en", "No Stripe subscription item found." } }),

            ["subscription.notification.payment_failed"] = ("Your payment failed. Your current plan is in a grace period.", new() { { "fr", "Votre paiement a échoué. Votre offre actuelle est en période de grâce." }, { "en", "Your payment failed. Your current plan is in a grace period." } }),
            ["subscription.notification.subscription_ended"] = ("Your subscription has ended. You have been switched to the free plan.", new() { { "fr", "Votre souscription a pris fin. Vous êtes repassé sur l'offre gratuite." }, { "en", "Your subscription has ended. You have been switched to the free plan." } }),
            ["subscription.error.unauthorized"] = ("Unauthorized access", new() { { "fr", "Accès non autorisé" }, { "en", "Unauthorized access" } }),
            ["subscription.error.not_found"] = ("Subscription not found.", new() { { "fr", "Abonnement introuvable." }, { "en", "Subscription not found." } }),

            ["plan.error.not_found"] = ("Plan not found.", new() { { "fr", "Offre introuvable." }, { "en", "Plan not found." } }),

            ["user.error.email_already_taken"] = ("This email is already in use", new() { { "fr", "Cet e-mail est déjà utilisé" }, { "en", "This email is already in use" } }),
            ["user.error.not_found"] = ("User not found", new() { { "fr", "Utilisateur introuvable" }, { "en", "User not found" } }),

            ["template.error.name_required"] = ("Template name is required", new() { { "fr", "Le nom du modèle est requis" }, { "en", "Template name is required" } }),
            ["template.error.name_max_length"] = ("Template name cannot exceed 150 characters", new() { { "fr", "Le nom du modèle ne peut pas dépasser 150 caractères" }, { "en", "Template name cannot exceed 150 characters" } }),
            ["template.error.json_data_required"] = ("JSON data is required", new() { { "fr", "Les données JSON sont requises" }, { "en", "JSON data is required" } }),
            ["template.error.not_found"] = ("Template not found", new() { { "fr", "Modèle introuvable" }, { "en", "Template not found" } }),

            ["job.error.invalid_limit"] = ("Limit must be greater than zero", new() { { "fr", "La limite doit être supérieure à zéro" }, { "en", "Limit must be greater than zero" } }),
            ["job.error.not_found"] = ("Print job not found", new() { { "fr", "Tâche d'impression introuvable" }, { "en", "Print job not found" } }),

            ["printer.error.invalid_protocol"] = ("Invalid protocol selected", new() { { "fr", "Protocole sélectionné invalide" }, { "en", "Invalid protocol selected" } }),
            ["printer.error.invalid_resolution"] = ("Invalid resolution selected", new() { { "fr", "Résolution sélectionnée invalide" }, { "en", "Invalid resolution selected" } }),
            ["printer.error.not_found"] = ("Printer not found", new() { { "fr", "Imprimante introuvable" }, { "en", "Printer not found" } }),

            ["language.error.invalid"] = ("Unsupported language", new() { { "fr", "Langue non supportée" }, { "en", "Unsupported language" } }),

            ["print.error.agent_not_connected"] = ("The print agent is not connected", new() { { "fr", "L'agent d'impression n'est pas connecté" }, { "en", "The print agent is not connected" } }),
            ["print.error.template_not_found"] = ("Template not found", new() { { "fr", "Modèle introuvable" }, { "en", "Template not found" } }),
            ["print.error.timeout"] = ("Timeout: The local agent did not respond", new() { { "fr", "Délai dépassé : L'agent local n'a pas répondu" }, { "en", "Timeout: The local agent did not respond" } }),

            ["auth.error.account_inactive"] = ("Account is inactive", new() { { "fr", "Votre compte est inactif" }, { "en", "Account is inactive" } }),
            ["auth.error.invalid_token"] = ("Invalid token", new() { { "fr", "Jeton invalide" }, { "en", "Invalid token" } }),

            ["agents.error.invalid_type"] = ("Invalid agent type selected", new() { { "fr", "Type d'agent sélectionné invalide" }, { "en", "Invalid agent type selected" } }),
            ["agent.error.not_found"] = ("Agent not found", new() { { "fr", "Agent introuvable" }, { "en", "Agent not found" } })
        };

        await SyncTranslationsAsync(translations);
    }
}