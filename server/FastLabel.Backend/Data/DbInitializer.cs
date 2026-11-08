using FastLabel.Backend.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace FastLabel.Backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // Fonction d'aide pour créer un Text avec ses traductions (DefaultVal en Anglais)
        async Task<Text> CreateTextWithTranslationsAsync(string tag, string defaultEnglishVal, Dictionary<string, string> translations)
        {
            var textEntity = await context.Texts
                .Include(t => t.Translations)
                .FirstOrDefaultAsync(t => t.Tag == tag);

            if (textEntity == null)
            {
                textEntity = new Text
                {
                    Tag = tag,
                    DefaultTranslation = defaultEnglishVal
                };
                context.Texts.Add(textEntity);
                await context.SaveChangesAsync();
            }

            foreach (var kvp in translations)
            {
                var langCode = kvp.Key;
                var translatedValue = kvp.Value;

                var lang = await context.PreferredLanguages.FirstOrDefaultAsync(l => l.Locale == langCode);
                if (lang != null)
                {
                    bool translationExists = await context.Translations
                        .AnyAsync(tr => tr.TextId == textEntity.Id && tr.PreferredLanguageId == lang.Id);

                    if (!translationExists)
                    {
                        context.Translations.Add(new Translation
                        {
                            TextId = textEntity.Id,
                            PreferredLanguageId = lang.Id,
                            TranslatedValue = translatedValue
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
            return textEntity;
        }

        // 1. Création préalable des Textes de description des langues (pour satisfaire la FK)
        var descFr = await CreateTextWithTranslationsAsync("lang.fr.desc", "French language", new() { { "fr_FR", "Langue française" }, { "en_GB", "French language" } });
        var descEn = await CreateTextWithTranslationsAsync("lang.en.desc", "English language", new() { { "fr_FR", "Langue anglaise" }, { "en_GB", "English language" } });

        // 2. Langues préférées (PreferredLanguage) insérées APRÈS leurs textes de description
        if (!await context.PreferredLanguages.AnyAsync())
        {
            context.PreferredLanguages.AddRange(
                new PreferredLanguage { Locale = "fr_FR", DescriptionTextId = descFr.Id, Flag = null },
                new PreferredLanguage { Locale = "en_GB", DescriptionTextId = descEn.Id, Flag = null }
            );
            await context.SaveChangesAsync();
        }

        // 3. Protocoles d'impression (Protocol)
        if (!await context.Protocols.AnyAsync())
        {
            var p1 = await CreateTextWithTranslationsAsync("protocol.rasterized_zpl", "Rasterized ZPL", new() { { "fr_FR", "ZPL Rasterisé" }, { "en_GB", "Rasterized ZPL" } });
            var p2 = await CreateTextWithTranslationsAsync("protocol.native_zpl", "Native ZPL", new() { { "fr_FR", "ZPL Natif" }, { "en_GB", "Native ZPL" } });

            context.Protocols.AddRange(
                new Protocol { NameTextId = p1.Id },
                new Protocol { NameTextId = p2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 4. Résolutions DPI (ResolutionDpi)
        if (!await context.ResolutionDpis.AnyAsync())
        {
            context.ResolutionDpis.AddRange(
                new ResolutionDpi { Value = 203 },
                new ResolutionDpi { Value = 300 },
                new ResolutionDpi { Value = 600 }
            );
            await context.SaveChangesAsync();
        }

        // 5. Types d'agents (AgentType)
        if (!await context.AgentTypes.AnyAsync())
        {
            var a1 = await CreateTextWithTranslationsAsync("agenttype.windows", "FastLabel Windows Agent", new() { { "fr_FR", "Agent Windows FastLabel" }, { "en_GB", "FastLabel Windows Agent" } });
            var a2 = await CreateTextWithTranslationsAsync("agenttype.linux", "FastLabel Linux Agent", new() { { "fr_FR", "Agent Linux FastLabel" }, { "en_GB", "FastLabel Linux Agent" } });
            var a3 = await CreateTextWithTranslationsAsync("agenttype.browser", "Web Browser", new() { { "fr_FR", "Navigateur Web" }, { "en_GB", "Web Browser" } });

            context.AgentTypes.AddRange(
                new AgentType { NameTextId = a1.Id },
                new AgentType { NameTextId = a2.Id },
                new AgentType { NameTextId = a3.Id }
            );
            await context.SaveChangesAsync();
        }

        // 6. Statuts de base (BasicStatus)
        if (!await context.BasicStatuses.AnyAsync())
        {
            var s1 = await CreateTextWithTranslationsAsync("status.active", "Active", new() { { "fr_FR", "Actif" }, { "en_GB", "Active" } });
            var s2 = await CreateTextWithTranslationsAsync("status.inactive", "Inactive", new() { { "fr_FR", "Inactif" }, { "en_GB", "Inactive" } });

            context.BasicStatuses.AddRange(
                new BasicStatus { NameTextId = s1.Id },
                new BasicStatus { NameTextId = s2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 7. Statuts des tâches d'impression (PrintJobStatus)
        if (!await context.PrintJobStatuses.AnyAsync())
        {
            var j1 = await CreateTextWithTranslationsAsync("jobstatus.created", "Created", new() { { "fr_FR", "Créé" }, { "en_GB", "Created" } });
            var j2 = await CreateTextWithTranslationsAsync("jobstatus.inprogress", "In Progress", new() { { "fr_FR", "En Cours" }, { "en_GB", "In Progress" } });
            var j3 = await CreateTextWithTranslationsAsync("jobstatus.completed", "Completed", new() { { "fr_FR", "Terminé" }, { "en_GB", "Completed" } });
            var j4 = await CreateTextWithTranslationsAsync("jobstatus.suspended", "Suspended", new() { { "fr_FR", "Suspendu" }, { "en_GB", "Suspended" } });

            context.PrintJobStatuses.AddRange(
                new PrintJobStatus { NameTextId = j1.Id },
                new PrintJobStatus { NameTextId = j2.Id },
                new PrintJobStatus { NameTextId = j3.Id },
                new PrintJobStatus { NameTextId = j4.Id }
            );
            await context.SaveChangesAsync();
        }

        // 8. Types d'abonnements (SubscriptionType)
        if (!await context.SubscriptionTypes.AnyAsync())
        {
            var sub1 = await CreateTextWithTranslationsAsync("subtype.paid", "Paid", new() { { "fr_FR", "Payé" }, { "en_GB", "Paid" } });
            var sub2 = await CreateTextWithTranslationsAsync("subtype.trial", "Trial", new() { { "fr_FR", "Essai" }, { "en_GB", "Trial" } });

            context.SubscriptionTypes.AddRange(
                new SubscriptionType { NameTextId = sub1.Id },
                new SubscriptionType { NameTextId = sub2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 9. Plans d'abonnement (Plan)
        if (!await context.Plans.AnyAsync())
        {
            var planFreeText = await CreateTextWithTranslationsAsync("plan.free.name", "Free Plan", new() { { "fr_FR", "Plan Gratuit" }, { "en_GB", "Free Plan" } });
            var planProText = await CreateTextWithTranslationsAsync("plan.pro.name", "Pro Plan", new() { { "fr_FR", "Plan Professionnel" }, { "en_GB", "Pro Plan" } });
            var planEnterpriseText = await CreateTextWithTranslationsAsync("plan.enterprise.name", "Enterprise Plan", new() { { "fr_FR", "Plan Entreprise" }, { "en_GB", "Enterprise Plan" } });

            var activeStatus = await context.BasicStatuses.FirstOrDefaultAsync(); 
            int statusId = activeStatus?.Id ?? 1;

            context.Plans.AddRange(
                new Plan 
                { 
                    NameTextId = planFreeText.Id, 
                    DescriptionJson = "{\"price\": 0, \"currency\": \"EUR\", \"billing\": \"monthly\", \"max_printers\": 2, \"features\": [\"Basic printing\", \"Up to 2 printers\"]}", 
                    BasicStatusId = statusId 
                },
                new Plan 
                { 
                    NameTextId = planProText.Id, 
                    DescriptionJson = "{\"price\": 29, \"currency\": \"EUR\", \"billing\": \"monthly\", \"max_printers\": 10, \"features\": [\"Unlimited printing\", \"Multiple agents\", \"Priority support\"]}", 
                    BasicStatusId = statusId 
                },
                new Plan 
                { 
                    NameTextId = planEnterpriseText.Id, 
                    DescriptionJson = "{\"price\": 99, \"currency\": \"EUR\", \"billing\": \"monthly\", \"max_printers\": -1, \"features\": [\"Unlimited printers\", \"Multi-site\", \"Dedicated API\", \"24/7 support\"]}", 
                    BasicStatusId = statusId 
                }
            );
            await context.SaveChangesAsync();
        }

        // 10. Compte Administrateur par défaut
        if (!await context.Users.AnyAsync())
        {
            var frenchLang = await context.PreferredLanguages.FirstOrDefaultAsync(l => l.Locale == "fr_FR");
            int langId = frenchLang?.Id ?? 1;

            var activeStatus = await context.BasicStatuses.FirstOrDefaultAsync();
            int statusId = activeStatus?.Id ?? 1;

            context.Users.Add(new User
            {
                Name = "Denis Clouet",
                Email = "denis.clouet@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("08071979"),
                PreferredLanguageId = langId,
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true
            });

            await context.SaveChangesAsync();
        }
    }
}