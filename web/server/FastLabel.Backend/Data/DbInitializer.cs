using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, IPasswordHasher<User>? passwordHasher = null)
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

                var lang = await context.Languages.FirstOrDefaultAsync(l => l.Code == langCode);
                if (lang != null)
                {
                    var existingTranslation = textEntity.Translations
                        .FirstOrDefault(tr => tr.LanguageId == lang.Id);

                    if (existingTranslation == null)
                    {
                        context.Translations.Add(new Translation
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

            await context.SaveChangesAsync();
            return textEntity;
        }

        // 1. Création préalable des Textes de description des langues (pour satisfaire la FK)
        var descFr = await CreateTextWithTranslationsAsync("lang.fr.desc", "French language", new() { { "fr", "Langue française" }, { "en", "French language" } });
        var descEn = await CreateTextWithTranslationsAsync("lang.en.desc", "English language", new() { { "fr", "Langue anglaise" }, { "en", "English language" } });

        // 2. Langues insérées APRÈS leurs textes de description
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.AddRange(
                new Language { Code = "fr", DescriptionTextId = descFr.Id, Flag = null },
                new Language { Code = "en", DescriptionTextId = descEn.Id, Flag = null }
            );
            await context.SaveChangesAsync();
        }

        // 3. Protocoles d'impression (Protocol)
        if (!await context.Protocols.AnyAsync())
        {
            var p1 = await CreateTextWithTranslationsAsync("protocol.rasterized_zpl", "Rasterized ZPL", new() { { "fr", "ZPL Rasterisé" }, { "en", "Rasterized ZPL" } });
            var p2 = await CreateTextWithTranslationsAsync("protocol.native_zpl", "Native ZPL", new() { { "fr", "ZPL Natif" }, { "en", "Native ZPL" } });

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
            var a1 = await CreateTextWithTranslationsAsync("agenttype.windows", "FastLabel Windows Agent", new() { { "fr", "Agent Windows FastLabel" }, { "en", "FastLabel Windows Agent" } });
            var a2 = await CreateTextWithTranslationsAsync("agenttype.linux", "FastLabel Linux Agent", new() { { "fr", "Agent Linux FastLabel" }, { "en", "FastLabel Linux Agent" } });
            var a3 = await CreateTextWithTranslationsAsync("agenttype.browser", "Web Browser", new() { { "fr", "Navigateur Web" }, { "en", "Web Browser" } });

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
            var s1 = await CreateTextWithTranslationsAsync("status.active", "Active", new() { { "fr", "Actif" }, { "en", "Active" } });
            var s2 = await CreateTextWithTranslationsAsync("status.inactive", "Inactive", new() { { "fr", "Inactif" }, { "en", "Inactive" } });

            context.BasicStatuses.AddRange(
                new BasicStatus { NameTextId = s1.Id },
                new BasicStatus { NameTextId = s2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 7. Statuts des tâches d'impression (PrintJobStatus)
        if (!await context.PrintJobStatuses.AnyAsync())
        {
            var j1 = await CreateTextWithTranslationsAsync("jobstatus.created", "Created", new() { { "fr", "Créé" }, { "en", "Created" } });
            var j2 = await CreateTextWithTranslationsAsync("jobstatus.inprogress", "In Progress", new() { { "fr", "En Cours" }, { "en", "In Progress" } });
            var j3 = await CreateTextWithTranslationsAsync("jobstatus.completed", "Completed", new() { { "fr", "Terminé" }, { "en", "Completed" } });
            var j4 = await CreateTextWithTranslationsAsync("jobstatus.suspended", "Suspended", new() { { "fr", "Suspendu" }, { "en", "Suspended" } });

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
            var sub1 = await CreateTextWithTranslationsAsync("subtype.paid", "Paid", new() { { "fr", "Payé" }, { "en", "Paid" } });
            var sub2 = await CreateTextWithTranslationsAsync("subtype.trial", "Trial", new() { { "fr", "Essai" }, { "en", "Trial" } });

            context.SubscriptionTypes.AddRange(
                new SubscriptionType { NameTextId = sub1.Id },
                new SubscriptionType { NameTextId = sub2.Id }
            );
            await context.SaveChangesAsync();
        }

        // 9. Plans d'abonnement (Plan)
        if (!await context.Plans.AnyAsync())
        {
            var planFreeText = await CreateTextWithTranslationsAsync("plan.free.name", "Free Plan", new() { { "fr", "Plan Gratuit" }, { "en", "Free Plan" } });
            var planProText = await CreateTextWithTranslationsAsync("plan.pro.name", "Pro Plan", new() { { "fr", "Plan Professionnel" }, { "en", "Pro Plan" } });
            var planEnterpriseText = await CreateTextWithTranslationsAsync("plan.enterprise.name", "Enterprise Plan", new() { { "fr", "Plan Entreprise" }, { "en", "Enterprise Plan" } });

            var activeStatus = await context.BasicStatuses.OrderBy(s => s.Id).FirstOrDefaultAsync();
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

            passwordHasher ??= new PasswordHasher<User>();

            var adminUser = new User
            {
                Name = "Denis Clouet",
                Email = "denis.clouet@gmail.com",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "08071979");

            var georgeUser = new User
            {
                Name = "George Harrison",
                Email = "georges.harrison4@gmail.com",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false
            };
            georgeUser.PasswordHash = passwordHasher.HashPassword(georgeUser, "08071979");

            context.Users.AddRange(adminUser, georgeUser);
            await context.SaveChangesAsync();
        }

        // 11. Textes UI
        await CreateTextWithTranslationsAsync("history.title", "Print History", new() { { "fr", "Historique des impressions" }, { "en", "Print History" } });
        await CreateTextWithTranslationsAsync("history.description", "Tracking and logs of labels sent to printers.", new() { { "fr", "Suivi et journaux des étiquettes envoyées aux imprimantes." }, { "en", "Tracking and logs of labels sent to printers." } });
        await CreateTextWithTranslationsAsync("history.no_history", "No print history at the moment.", new() { { "fr", "Aucun historique d'impression pour l'instant." }, { "en", "No print history at the moment." } });

        await CreateTextWithTranslationsAsync("users.title", "User Management", new() { { "fr", "Gestion des utilisateurs" }, { "en", "User Management" } });
        await CreateTextWithTranslationsAsync("users.description", "List of users with access to the FastLabel platform.", new() { { "fr", "Liste des utilisateurs ayant accès à la plateforme FastLabel." }, { "en", "List of users with access to the FastLabel platform." } });
        await CreateTextWithTranslationsAsync("users.no_users", "No other registered users.", new() { { "fr", "Aucun autre utilisateur enregistré." }, { "en", "No other registered users." } });

        await CreateTextWithTranslationsAsync("agents.title", "ZPL Agents", new() { { "fr", "Agents ZPL" }, { "en", "ZPL Agents" } });
        await CreateTextWithTranslationsAsync("agents.description", "Status of your connected Windows agents.", new() { { "fr", "État de vos agents Windows connectés." }, { "en", "Status of your connected Windows agents." } });
        await CreateTextWithTranslationsAsync("agents.no_agents", "No active agents at the moment.", new() { { "fr", "Aucun agent actif pour le moment." }, { "en", "No active agents at the moment." } });

        await CreateTextWithTranslationsAsync("printers.title", "Thermal Printers", new() { { "fr", "Imprimantes Thermiques" }, { "en", "Thermal Printers" } });
        await CreateTextWithTranslationsAsync("printers.description", "List of connected and configured printers.", new() { { "fr", "Liste des imprimantes connectées et configurées." }, { "en", "List of connected and configured printers." } });
        await CreateTextWithTranslationsAsync("printers.no_printers", "No printers detected.", new() { { "fr", "Aucune imprimante détectée." }, { "en", "No printers detected." } });

        await CreateTextWithTranslationsAsync("templates.title", "Label Templates", new() { { "fr", "Modèles d'étiquettes" }, { "en", "Label Templates" } });
        await CreateTextWithTranslationsAsync("templates.description", "Manage and design your ZPL label templates.", new() { { "fr", "Gérez et concevez vos modèles d'étiquettes ZPL." }, { "en", "Manage and design your ZPL label templates." } });
        await CreateTextWithTranslationsAsync("templates.no_templates", "No label templates saved at the moment.", new() { { "fr", "Aucun modèle d'étiquette enregistré pour le moment." }, { "en", "No label templates saved at the moment." } });

        await CreateTextWithTranslationsAsync("nav.home", "Home", new() { { "fr", "Accueil" }, { "en", "Home" } });
        await CreateTextWithTranslationsAsync("nav.templates", "Label Templates", new() { { "fr", "Modèles d'étiquettes" }, { "en", "Label Templates" } });
        await CreateTextWithTranslationsAsync("nav.printers", "Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } });
        await CreateTextWithTranslationsAsync("nav.agents", "Agents", new() { { "fr", "Agents" }, { "en", "Agents" } });
        await CreateTextWithTranslationsAsync("nav.users", "Users", new() { { "fr", "Utilisateurs" }, { "en", "Users" } });
        await CreateTextWithTranslationsAsync("nav.history", "History", new() { { "fr", "Historique" }, { "en", "History" } });
        await CreateTextWithTranslationsAsync("nav.my_account", "My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } });
        await CreateTextWithTranslationsAsync("nav.logout", "Logout", new() { { "fr", "Déconnexion" }, { "en", "Logout" } });

        await CreateTextWithTranslationsAsync("account.title", "My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } });
        await CreateTextWithTranslationsAsync("account.personal_info", "Personal Information", new() { { "fr", "Informations personnelles" }, { "en", "Personal Information" } });
        await CreateTextWithTranslationsAsync("account.email_label", "Email Address", new() { { "fr", "Adresse Email" }, { "en", "Email Address" } });
        await CreateTextWithTranslationsAsync("account.role_label", "Role / Plan", new() { { "fr", "Rôle / Plan" }, { "en", "Role / Plan" } });
        await CreateTextWithTranslationsAsync("account.preferences_title", "User Preferences", new() { { "fr", "Préférences utilisateur" }, { "en", "User Preferences" } });
        await CreateTextWithTranslationsAsync("account.email_notifications", "Receive email notifications", new() { { "fr", "Recevoir les notifications par email" }, { "en", "Receive email notifications" } });
        await CreateTextWithTranslationsAsync("account.default_printer", "Default ZPL Printer", new() { { "fr", "Imprimante ZPL par défaut" }, { "en", "Default ZPL Printer" } });
        await CreateTextWithTranslationsAsync("account.saving", "Saving...", new() { { "fr", "Enregistrement..." }, { "en", "Saving..." } });
        await CreateTextWithTranslationsAsync("account.save_button", "Save Changes", new() { { "fr", "Enregistrer les modifications" }, { "en", "Save Changes" } });
        await CreateTextWithTranslationsAsync("account.loading", "Loading...", new() { { "fr", "Chargement en cours..." }, { "en", "Loading..." } });
        await CreateTextWithTranslationsAsync("account.success_saved", "Your preferences have been successfully saved.", new() { { "fr", "Vos préférences ont été enregistrées avec succès." }, { "en", "Your preferences have been successfully saved." } });
        await CreateTextWithTranslationsAsync("account.error_save_failed", "Error while saving preferences.", new() { { "fr", "Erreur lors de l'enregistrement des préférences." }, { "en", "Error while saving preferences." } });

        await CreateTextWithTranslationsAsync("dashboard.title", "FastLabel - Dashboard", new() { { "fr", "FastLabel - Tableau de bord" }, { "en", "FastLabel - Dashboard" } });
        await CreateTextWithTranslationsAsync("dashboard.agents_header", "ZPL Agents & Printers", new() { { "fr", "Agents & Imprimantes ZPL" }, { "en", "ZPL Agents & Printers" } });
        await CreateTextWithTranslationsAsync("dashboard.agents_description", "Management and status of your connected Windows agents for thermal printing.", new() { { "fr", "Gestion et état de vos agents Windows connectés pour l'impression thermique." }, { "en", "Management and status of your connected Windows agents for thermal printing." } });
        await CreateTextWithTranslationsAsync("dashboard.no_active_agents", "No active agents at the moment. The ZPL listening service will communicate here soon.", new() { { "fr", "Aucun agent actif pour le moment. Le service d'écoute ZPL communiquera bientôt ici." }, { "en", "No active agents at the moment. The ZPL listening service will communicate here soon." } });

        await CreateTextWithTranslationsAsync("auth.error.invalid_or_expired_token", "Invalid or expired reset token.", new() { { "fr", "Jeton de réinitialisation invalide ou expiré." }, { "en", "Invalid or expired reset token." } });
        await CreateTextWithTranslationsAsync("auth.reset_password_title", "Reset your password", new() { { "fr", "Réinitialiser votre mot de passe" }, { "en", "Reset your password" } });
        await CreateTextWithTranslationsAsync("auth.new_password", "New password", new() { { "fr", "Nouveau mot de passe" }, { "en", "New password" } });
        await CreateTextWithTranslationsAsync("auth.success.password_reset", "Your password has been successfully reset. You can now log in.", new() { { "fr", "Votre mot de passe a été réinitialisé avec succès. Vous pouvez maintenant vous connecter." }, { "en", "Your password has been successfully reset. You can now log in." } });
        await CreateTextWithTranslationsAsync("auth.error.missing_token", "Invalid or missing reset token.", new() { { "fr", "Jeton de réinitialisation invalide ou manquant." }, { "en", "Invalid or missing reset token." } });
        await CreateTextWithTranslationsAsync("auth.success.account_activated", "Your account has been successfully activated. You can now log in.", new() { { "fr", "Votre compte a été activé avec succès. Vous pouvez maintenant vous connecter." }, { "en", "Your account has been successfully activated. You can now log in." } });
        await CreateTextWithTranslationsAsync("auth.error.email_already_taken", "This email is already taken.", new() { { "fr", "Cet email est déjà utilisé." }, { "en", "This email is already taken." } });
        await CreateTextWithTranslationsAsync("auth.success.registered", "Registration successful.", new() { { "fr", "Inscription réussie." }, { "en", "Registration successful." } });
        await CreateTextWithTranslationsAsync("auth.name", "Full Name", new() { { "fr", "Nom complet" }, { "en", "Full Name" } });
        await CreateTextWithTranslationsAsync("auth.name_placeholder", "John Doe", new() { { "fr", "Jean Dupont" }, { "en", "John Doe" } });
        await CreateTextWithTranslationsAsync("auth.error.invalid_credentials", "Invalid credentials.", new() { { "fr", "Identifiants invalides." }, { "en", "Invalid credentials." } });
        await CreateTextWithTranslationsAsync("auth.error.server_unreachable", "Unable to reach the server.", new() { { "fr", "Impossible de joindre le serveur." }, { "en", "Unable to reach the server." } });
        await CreateTextWithTranslationsAsync("auth.sending", "Sending...", new() { { "fr", "Envoi en cours..." }, { "en", "Sending..." } });
        await CreateTextWithTranslationsAsync("auth.forgot_success_message", "If an account matches this email, a recovery link has been sent.", new() { { "fr", "Si un compte est associé à cet email, un lien de réinitialisation a été envoyé." }, { "en", "If an account matches this email, a recovery link has been sent." } });
        await CreateTextWithTranslationsAsync("auth.error.request_failed", "An error occurred while processing your request.", new() { { "fr", "Une erreur est survenue lors de la demande." }, { "en", "An error occurred while processing your request." } });
        await CreateTextWithTranslationsAsync("auth.error.registration_failed", "An error occurred during registration.", new() { { "fr", "Erreur lors de l'inscription." }, { "en", "An error occurred during registration." } });
        await CreateTextWithTranslationsAsync("auth.login_title", "Login", new() { { "fr", "Connexion" }, { "en", "Login" } });
        await CreateTextWithTranslationsAsync("auth.email", "Email", new() { { "fr", "Email" }, { "en", "Email" } });
        await CreateTextWithTranslationsAsync("auth.password", "Password", new() { { "fr", "Mot de passe" }, { "en", "Password" } });
        await CreateTextWithTranslationsAsync("auth.submit", "Sign In", new() { { "fr", "Se connecter" }, { "en", "Sign In" } });
        await CreateTextWithTranslationsAsync("auth.create_account", "Create an account", new() { { "fr", "Créer un compte" }, { "en", "Create an account" } });
        await CreateTextWithTranslationsAsync("auth.forgot_password", "Forgot password?", new() { { "fr", "Mot de passe oublié ?" }, { "en", "Forgot password?" } });
        await CreateTextWithTranslationsAsync("auth.register_title", "Register", new() { { "fr", "Inscription" }, { "en", "Register" } });
        await CreateTextWithTranslationsAsync("auth.register_button", "Sign Up", new() { { "fr", "S'inscrire" }, { "en", "Sign Up" } });
        await CreateTextWithTranslationsAsync("auth.already_have_account", "Already have an account? Sign in", new() { { "fr", "Déjà un compte ? Connectez-vous" }, { "en", "Already have an account? Sign in" } });
        await CreateTextWithTranslationsAsync("auth.forgot_heading", "Forgot password", new() { { "fr", "Mot de passe oublié" }, { "en", "Forgot password" } });
        await CreateTextWithTranslationsAsync("auth.forgot_instruction", "Enter your email address to receive password reset instructions.", new() { { "fr", "Entre ton adresse email pour recevoir les instructions de réinitialisation." }, { "en", "Enter your email address to receive password reset instructions." } });
        await CreateTextWithTranslationsAsync("auth.email_placeholder", "name@example.com", new() { { "fr", "nom@exemple.com" }, { "en", "name@example.com" } });
        await CreateTextWithTranslationsAsync("auth.send_link_button", "Send Link", new() { { "fr", "Envoyer le lien" }, { "en", "Send Link" } });
        await CreateTextWithTranslationsAsync("auth.back_to_login", "Back to login", new() { { "fr", "Retour à la connexion" }, { "en", "Back to login" } });
        await CreateTextWithTranslationsAsync("auth.email_placeholder_alt", "votre@email.com", new() { { "fr", "votre@email.com" }, { "en", "your@email.com" } });

        // 12. Emails
        await CreateTextWithTranslationsAsync("email.activation.subject", "Activate your FastLabel account", new() { { "fr", "Activez votre compte FastLabel" }, { "en", "Activate your FastLabel account" } });
        await CreateTextWithTranslationsAsync("email.activation.body", "Hello {0},\n\nPlease click the following link to activate your account:\n{1}", new() { { "fr", "Bonjour {0},\n\nVeuillez cliquer sur le lien suivant pour activer votre compte :\n{1}" }, { "en", "Hello {0},\n\nPlease click the following link to activate your account:\n{1}" } });
         await CreateTextWithTranslationsAsync("email.reset.subject", "Reset your FastLabel password", new() { { "fr", "Réinitialisez votre mot de passe FastLabel" }, { "en", "Reset your FastLabel password" } });
        await CreateTextWithTranslationsAsync("email.reset.body", "Hello {0},\n\nPlease click the following link to reset your password:\n{1}", new() { { "fr", "Bonjour {0},\n\nVeuillez cliquer sur le lien suivant pour réinitialiser votre mot de passe :\n{1}" }, { "en", "Hello {0},\n\nPlease click the following link to reset your password:\n{1}" } });
    }
}