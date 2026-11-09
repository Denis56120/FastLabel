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
                await context.SaveChangesAsync(); // Sauvegarde pour obtenir l'ID
            }

            // On vérifie et ajoute les traductions manquantes pour chaque langue
            foreach (var kvp in translations)
            {
                var langCode = kvp.Key;
                var translatedValue = kvp.Value;

                var lang = await context.PreferredLanguages.FirstOrDefaultAsync(l => l.Locale == langCode);
                if (lang != null)
                {
                    var existingTranslation = textEntity.Translations
                        .FirstOrDefault(tr => tr.PreferredLanguageId == lang.Id);

                    if (existingTranslation == null)
                    {
                        context.Translations.Add(new Translation
                        {
                            TextId = textEntity.Id,
                            PreferredLanguageId = lang.Id,
                            TranslatedValue = translatedValue
                        });
                    }
                    else if (existingTranslation.TranslatedValue != translatedValue)
                    {
                        // Optionnel : met à jour si la valeur a changé dans ton code
                        existingTranslation.TranslatedValue = translatedValue;
                    }
                }
            }

            await context.SaveChangesAsync();
            return textEntity;
        }

        // 1. Création préalable des Textes de description des langues (pour satisfaire la FK)
        var descFr = await CreateTextWithTranslationsAsync("lang.fr.desc", "French language", new() { { "fr-FR", "Langue française" }, { "en-GB", "French language" } });
        var descEn = await CreateTextWithTranslationsAsync("lang.en.desc", "English language", new() { { "fr-FR", "Langue anglaise" }, { "en-GB", "English language" } });

        // 2. Langues préférées (PreferredLanguage) insérées APRÈS leurs textes de description
        if (!await context.PreferredLanguages.AnyAsync())
        {
            context.PreferredLanguages.AddRange(
                new PreferredLanguage { Locale = "fr-FR", DescriptionTextId = descFr.Id, Flag = null },
                new PreferredLanguage { Locale = "en-GB", DescriptionTextId = descEn.Id, Flag = null }
            );
            await context.SaveChangesAsync();
        }

        // 3. Protocoles d'impression (Protocol)
        if (!await context.Protocols.AnyAsync())
        {
            var p1 = await CreateTextWithTranslationsAsync("protocol.rasterized_zpl", "Rasterized ZPL", new() { { "fr-FR", "ZPL Rasterisé" }, { "en-GB", "Rasterized ZPL" } });
            var p2 = await CreateTextWithTranslationsAsync("protocol.native_zpl", "Native ZPL", new() { { "fr-FR", "ZPL Natif" }, { "en-GB", "Native ZPL" } });

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
            var a1 = await CreateTextWithTranslationsAsync("agenttype.windows", "FastLabel Windows Agent", new() { { "fr-FR", "Agent Windows FastLabel" }, { "en-GB", "FastLabel Windows Agent" } });
            var a2 = await CreateTextWithTranslationsAsync("agenttype.linux", "FastLabel Linux Agent", new() { { "fr-FR", "Agent Linux FastLabel" }, { "en-GB", "FastLabel Linux Agent" } });
            var a3 = await CreateTextWithTranslationsAsync("agenttype.browser", "Web Browser", new() { { "fr-FR", "Navigateur Web" }, { "en-GB", "Web Browser" } });

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
            var s1 = await CreateTextWithTranslationsAsync("status.active", "Active", new() { { "fr-FR", "Actif" }, { "en-GB", "Active" } });
            var s2 = await CreateTextWithTranslationsAsync("status.inactive", "Inactive", new() { { "fr-FR", "Inactif" }, { "en-GB", "Inactive" } });

            context.BasicStatuses.AddRange(
                new BasicStatus { NameTextId = s1.Id },
                new BasicStatus { NameTextId = s2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 7. Statuts des tâches d'impression (PrintJobStatus)
        if (!await context.PrintJobStatuses.AnyAsync())
        {
            var j1 = await CreateTextWithTranslationsAsync("jobstatus.created", "Created", new() { { "fr-FR", "Créé" }, { "en-GB", "Created" } });
            var j2 = await CreateTextWithTranslationsAsync("jobstatus.inprogress", "In Progress", new() { { "fr-FR", "En Cours" }, { "en-GB", "In Progress" } });
            var j3 = await CreateTextWithTranslationsAsync("jobstatus.completed", "Completed", new() { { "fr-FR", "Terminé" }, { "en-GB", "Completed" } });
            var j4 = await CreateTextWithTranslationsAsync("jobstatus.suspended", "Suspended", new() { { "fr-FR", "Suspendu" }, { "en-GB", "Suspended" } });

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
            var sub1 = await CreateTextWithTranslationsAsync("subtype.paid", "Paid", new() { { "fr-FR", "Payé" }, { "en-GB", "Paid" } });
            var sub2 = await CreateTextWithTranslationsAsync("subtype.trial", "Trial", new() { { "fr-FR", "Essai" }, { "en-GB", "Trial" } });

            context.SubscriptionTypes.AddRange(
                new SubscriptionType { NameTextId = sub1.Id },
                new SubscriptionType { NameTextId = sub2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 9. Plans d'abonnement (Plan)
        if (!await context.Plans.AnyAsync())
        {
            var planFreeText = await CreateTextWithTranslationsAsync("plan.free.name", "Free Plan", new() { { "fr-FR", "Plan Gratuit" }, { "en-GB", "Free Plan" } });
            var planProText = await CreateTextWithTranslationsAsync("plan.pro.name", "Pro Plan", new() { { "fr-FR", "Plan Professionnel" }, { "en-GB", "Pro Plan" } });
            var planEnterpriseText = await CreateTextWithTranslationsAsync("plan.enterprise.name", "Enterprise Plan", new() { { "fr-FR", "Plan Entreprise" }, { "en-GB", "Enterprise Plan" } });

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
            var activeStatus = await context.BasicStatuses.FirstOrDefaultAsync();
            int statusId = activeStatus?.Id ?? 1;

            context.Users.AddRange(
                new User
                {
                    Name = "Denis Clouet",
                    Email = "denis.clouet@gmail.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("08071979"),
                    BasicStatusId = statusId,
                    CreatedAt = DateTime.UtcNow,
                    IsAdmin = true
                },
                new User
                {
                    Name = "George Harrison",
                    Email = "georges.harrison4@gmail.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("08071979"),
                    BasicStatusId = statusId,
                    CreatedAt = DateTime.UtcNow,
                    IsAdmin = false
                }
            );

            await context.SaveChangesAsync();

            // 11. Textes UI
            await CreateTextWithTranslationsAsync("nav.home", "Home", new() { { "fr-FR", "Accueil" }, { "en-GB", "Home" } });
            await CreateTextWithTranslationsAsync("nav.templates", "Label Templates", new() { { "fr-FR", "Modèles d'étiquettes" }, { "en-GB", "Label Templates" } });
            await CreateTextWithTranslationsAsync("nav.printers", "Printers", new() { { "fr-FR", "Imprimantes" }, { "en-GB", "Printers" } });
            await CreateTextWithTranslationsAsync("nav.agents", "Agents", new() { { "fr-FR", "Agents" }, { "en-GB", "Agents" } });
            await CreateTextWithTranslationsAsync("nav.users", "Users", new() { { "fr-FR", "Utilisateurs" }, { "en-GB", "Users" } });
            await CreateTextWithTranslationsAsync("nav.history", "History", new() { { "fr-FR", "Historique" }, { "en-GB", "History" } });
            await CreateTextWithTranslationsAsync("nav.my_account", "My Account", new() { { "fr-FR", "Mon Compte" }, { "en-GB", "My Account" } });
            await CreateTextWithTranslationsAsync("nav.logout", "Logout", new() { { "fr-FR", "Déconnexion" }, { "en-GB", "Logout" } });

            await CreateTextWithTranslationsAsync("account.title", "My Account", new() { { "fr-FR", "Mon Compte" }, { "en-GB", "My Account" } });
            await CreateTextWithTranslationsAsync("account.personal_info", "Personal Information", new() { { "fr-FR", "Informations personnelles" }, { "en-GB", "Personal Information" } });
            await CreateTextWithTranslationsAsync("account.email_label", "Email Address", new() { { "fr-FR", "Adresse Email" }, { "en-GB", "Email Address" } });
            await CreateTextWithTranslationsAsync("account.role_label", "Role / Plan", new() { { "fr-FR", "Rôle / Plan" }, { "en-GB", "Role / Plan" } });
            await CreateTextWithTranslationsAsync("account.preferences_title", "User Preferences", new() { { "fr-FR", "Préférences utilisateur" }, { "en-GB", "User Preferences" } });
            await CreateTextWithTranslationsAsync("account.email_notifications", "Receive email notifications", new() { { "fr-FR", "Recevoir les notifications par email" }, { "en-GB", "Receive email notifications" } });
            await CreateTextWithTranslationsAsync("account.default_printer", "Default ZPL Printer", new() { { "fr-FR", "Imprimante ZPL par défaut" }, { "en-GB", "Default ZPL Printer" } });
            await CreateTextWithTranslationsAsync("account.saving", "Saving...", new() { { "fr-FR", "Enregistrement..." }, { "en-GB", "Saving..." } });
            await CreateTextWithTranslationsAsync("account.save_button", "Save Changes", new() { { "fr-FR", "Enregistrer les modifications" }, { "en-GB", "Save Changes" } });

            await CreateTextWithTranslationsAsync("dashboard.title", "FastLabel - Dashboard", new() { { "fr-FR", "FastLabel - Tableau de bord" }, { "en-GB", "FastLabel - Dashboard" } });
            await CreateTextWithTranslationsAsync("dashboard.agents_header", "ZPL Agents & Printers", new() { { "fr-FR", "Agents & Imprimantes ZPL" }, { "en-GB", "ZPL Agents & Printers" } });
            await CreateTextWithTranslationsAsync("dashboard.agents_description", "Management and status of your connected Windows agents for thermal printing.", new() { { "fr-FR", "Gestion et état de vos agents Windows connectés pour l'impression thermique." }, { "en-GB", "Management and status of your connected Windows agents for thermal printing." } });
            await CreateTextWithTranslationsAsync("dashboard.no_active_agents", "No active agents at the moment. The ZPL listening service will communicate here soon.", new() { { "fr-FR", "Aucun agent actif pour le moment. Le service d'écoute ZPL communiquera bientôt ici." }, { "en-GB", "No active agents at the moment. The ZPL listening service will communicate here soon." } });

            await CreateTextWithTranslationsAsync("auth.login_title", "Login", new() { { "fr-FR", "Connexion" }, { "en-GB", "Login" } });
            await CreateTextWithTranslationsAsync("auth.email", "Email", new() { { "fr-FR", "Email" }, { "en-GB", "Email" } });
            await CreateTextWithTranslationsAsync("auth.password", "Password", new() { { "fr-FR", "Mot de passe" }, { "en-GB", "Password" } });
            await CreateTextWithTranslationsAsync("auth.submit", "Sign In", new() { { "fr-FR", "Se connecter" }, { "en-GB", "Sign In" } });
            await CreateTextWithTranslationsAsync("auth.create_account", "Create an account", new() { { "fr-FR", "Créer un compte" }, { "en-GB", "Create an account" } });
            await CreateTextWithTranslationsAsync("auth.forgot_password", "Forgot password?", new() { { "fr-FR", "Mot de passe oublié ?" }, { "en-GB", "Forgot password?" } });
            await CreateTextWithTranslationsAsync("auth.register_title", "Register", new() { { "fr-FR", "Inscription" }, { "en-GB", "Register" } });
            await CreateTextWithTranslationsAsync("auth.register_button", "Sign Up", new() { { "fr-FR", "S'inscrire" }, { "en-GB", "Sign Up" } });
            await CreateTextWithTranslationsAsync("auth.already_have_account", "Already have an account? Sign in", new() { { "fr-FR", "Déjà un compte ? Connectez-vous" }, { "en-GB", "Already have an account? Sign in" } });
            await CreateTextWithTranslationsAsync("auth.forgot_heading", "Forgot password", new() { { "fr-FR", "Mot de passe oublié" }, { "en-GB", "Forgot password" } });
            await CreateTextWithTranslationsAsync("auth.forgot_instruction", "Enter your email address to receive password reset instructions.", new() { { "fr-FR", "Entre ton adresse email pour recevoir les instructions de réinitialisation." }, { "en-GB", "Enter your email address to receive password reset instructions." } });
            await CreateTextWithTranslationsAsync("auth.email_placeholder", "name@example.com", new() { { "fr-FR", "nom@exemple.com" }, { "en-GB", "name@example.com" } });
            await CreateTextWithTranslationsAsync("auth.send_link_button", "Send Link", new() { { "fr-FR", "Envoyer le lien" }, { "en-GB", "Send Link" } });
            await CreateTextWithTranslationsAsync("auth.back_to_login", "Back to login", new() { { "fr-FR", "Retour à la connexion" }, { "en-GB", "Back to login" } });
            await CreateTextWithTranslationsAsync("auth.email_placeholder_alt", "votre@email.com", new() { { "fr-FR", "votre@email.com" }, { "en-GB", "your@email.com" } });
        }
    }
}