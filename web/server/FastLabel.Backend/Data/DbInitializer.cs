namespace FastLabel.Backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, IConfiguration configuration, IPasswordHasher<User>? passwordHasher = null)
    {
        await context.Database.EnsureCreatedAsync();

        async Task<Text> CreateTextWithTranslationsAsync(string tag, string defaultEnglishVal, Dictionary<string, string> translations)
        {
            var textEntity = await context.Texts
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
                context.Texts.Add(textEntity);
                await context.SaveChangesAsync(); 
            }

            foreach (var kvp in translations)
            {
                var langCode = kvp.Key;
                var translatedValue = kvp.Value;

                var lang = await context.Languages
                    .OrderBy(l => l.Id)
                    .FirstOrDefaultAsync(l => l.Code == langCode);
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

        // 1. Création préalable des Textes de description des langues
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
 
            context.AgentTypes.AddRange(
                new AgentType { NameTextId = a1.Id },
                new AgentType { NameTextId = a2.Id }
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
            var activeStatus = await context.BasicStatuses
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            int statusId = activeStatus?.Id ?? 1;

            passwordHasher ??= new PasswordHasher<User>();

            var adminUser = new User
            {
                Name = configuration["ADMIN_USER_NAME"] ?? string.Empty,
                Email = configuration["ADMIN_USER_EMAIL"] ?? string.Empty,
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, configuration["ADMIN_USER_PASSWORD"] ?? string.Empty);

            var regularUser = new User
            {
                Name = configuration["TEST_USER_NAME"] ?? string.Empty,
                Email = configuration["TEST_USER_EMAIL"] ?? string.Empty,
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false
            };
            regularUser.PasswordHash = passwordHasher.HashPassword(regularUser, configuration["TEST_USER_PASSWORD"] ?? string.Empty);

            context.Users.AddRange(adminUser, regularUser);
            await context.SaveChangesAsync();
        }

        // 11. Textes UI
        await CreateTextWithTranslationsAsync("app.error.unknown", "Une erreur inattendue s'est produite.", new() { { "fr", "Une erreur inattendue s'est produite." }, { "en", "An unexpected error occurred." } });
        await CreateTextWithTranslationsAsync("app.success.saved", "Saved successfully.", new() { { "fr", "Enregistré avec succès." }, { "en", "Saved successfully." } });
        await CreateTextWithTranslationsAsync("app.success.deleted", "Deleted successfully.", new() { { "fr", "Supprimé avec succès." }, { "en", "Deleted successfully." } });
        await CreateTextWithTranslationsAsync("app.success.login", "Login successful.", new() { { "fr", "Connexion réussie." }, { "en", "Login successful." } });

        await CreateTextWithTranslationsAsync("common.back", "Back", new() { { "fr", "Retour" }, { "en", "Back" } });
        await CreateTextWithTranslationsAsync("common.no_records", "No records found.", new() { { "fr", "Aucun enregistrement." }, { "en", "No records found." } });
        await CreateTextWithTranslationsAsync("common.form", "Form", new() { { "fr", "Formulaire" }, { "en", "Form" } });
        await CreateTextWithTranslationsAsync("common.add", "Add", new() { { "fr", "Ajouter" }, { "en", "Add" } });
        await CreateTextWithTranslationsAsync("common.edit", "Edit", new() { { "fr", "Modifier" }, { "en", "Edit" } });
        await CreateTextWithTranslationsAsync("common.delete", "Delete", new() { { "fr", "Supprimer" }, { "en", "Delete" } });
        await CreateTextWithTranslationsAsync("common.save", "Save", new() { { "fr", "Enregistrer" }, { "en", "Save" } });
        await CreateTextWithTranslationsAsync("common.cancel", "Cancel", new() { { "fr", "Annuler" }, { "en", "Cancel" } });
        await CreateTextWithTranslationsAsync("common.loading", "Loading...", new() { { "fr", "Chargement en cours..." }, { "en", "Loading..." } });

        await CreateTextWithTranslationsAsync("history.status.success", "Success", new() { { "fr", "Succès" }, { "en", "Success" } });
        await CreateTextWithTranslationsAsync("history.status.error", "Error", new() { { "fr", "Erreur" }, { "en", "Error" } });
        await CreateTextWithTranslationsAsync("history.title", "Print History", new() { { "fr", "Historique d'impression" }, { "en", "Print History" } });
        await CreateTextWithTranslationsAsync("history.description", "View and manage print job logs.", new() { { "fr", "Consultez et gérez l'historique des impressions." }, { "en", "View and manage print job logs." } });
        await CreateTextWithTranslationsAsync("history.no_history", "No print history available.", new() { { "fr", "Aucun historique d'impression disponible." }, { "en", "No print history available." } });
        await CreateTextWithTranslationsAsync("history.printer", "Printer", new() { { "fr", "Imprimante" }, { "en", "Printer" } });
        await CreateTextWithTranslationsAsync("history.template", "Template", new() { { "fr", "Modèle" }, { "en", "Template" } });
        await CreateTextWithTranslationsAsync("history.quantity", "Quantity", new() { { "fr", "Quantité" }, { "en", "Quantity" } });
        await CreateTextWithTranslationsAsync("history.status", "Status", new() { { "fr", "Statut" }, { "en", "Status" } });
        await CreateTextWithTranslationsAsync("history.date", "Date", new() { { "fr", "Date" }, { "en", "Date" } });
        await CreateTextWithTranslationsAsync("history.response_message", "Response Message", new() { { "fr", "Message de réponse" }, { "en", "Response Message" } });
        await CreateTextWithTranslationsAsync("history.add_title", "Add History Entry", new() { { "fr", "Ajouter une entrée" }, { "en", "Add History Entry" } });
        await CreateTextWithTranslationsAsync("history.edit_title", "Edit History Entry", new() { { "fr", "Modifier l'entrée" }, { "en", "Edit History Entry" } });

        await CreateTextWithTranslationsAsync("users.role_admin", "Admin", new() { { "fr", "Admin" }, { "en", "Admin" } });
        await CreateTextWithTranslationsAsync("users.role_user", "User", new() { { "fr", "Utilisateur" }, { "en", "User" } });
        await CreateTextWithTranslationsAsync("users.title", "Users Management", new() { { "fr", "Gestion des utilisateurs" }, { "en", "Users Management" } });
        await CreateTextWithTranslationsAsync("users.password", "Password", new() { { "fr", "Mot de passe" }, { "en", "Password" } });
        await CreateTextWithTranslationsAsync("users.password_help", "Leave blank to keep the current password when editing.", new() { { "fr", "Laisser vide pour conserver le mot de passe actuel lors d'une modification." }, { "en", "Leave blank to keep the current password when editing." } });
        await CreateTextWithTranslationsAsync("users.status", "Status", new() { { "fr", "Statut" }, { "en", "Status" } });
        await CreateTextWithTranslationsAsync("users.description", "Manage application users and permissions.", new() { { "fr", "Gérez les utilisateurs et leurs permissions." }, { "en", "Manage application users and permissions." } });
        await CreateTextWithTranslationsAsync("users.no_users", "No users found.", new() { { "fr", "Aucun utilisateur trouvé." }, { "en", "No users found." } });
        await CreateTextWithTranslationsAsync("users.name", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("users.email", "Email", new() { { "fr", "E-mail" }, { "en", "Email" } });
        await CreateTextWithTranslationsAsync("users.role", "Role", new() { { "fr", "Rôle" }, { "en", "Role" } });
        await CreateTextWithTranslationsAsync("users.created_at", "Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } });
        await CreateTextWithTranslationsAsync("users.is_admin", "Administrator", new() { { "fr", "Administrateur" }, { "en", "Administrator" } });
        await CreateTextWithTranslationsAsync("users.add_title", "Add User", new() { { "fr", "Ajouter un utilisateur" }, { "en", "Add User" } });
        await CreateTextWithTranslationsAsync("users.edit_title", "Edit User", new() { { "fr", "Modifier l'utilisateur" }, { "en", "Edit User" } });

        await CreateTextWithTranslationsAsync("agents.created_at", "Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } });
        await CreateTextWithTranslationsAsync("agents.error.type_required", "Please select an agent type", new() { { "fr", "Veuillez sélectionner un type d'agent" }, { "en", "Please select an agent type" } });
        await CreateTextWithTranslationsAsync("agents.api_key_readonly_help", "This key is automatically generated and cannot be modified.", new() { { "fr", "Cette clé est générée automatiquement et ne peut pas être modifiée." }, { "en", "This key is automatically generated and cannot be modified." } });
        await CreateTextWithTranslationsAsync("agents.select_type", "-- Select a type --", new() { { "fr", "-- Sélectionner un type --" }, { "en", "-- Select a type --" } });
        await CreateTextWithTranslationsAsync("agents.title", "Agents Management", new() { { "fr", "Gestion des agents" }, { "en", "Agents Management" } });
        await CreateTextWithTranslationsAsync("agents.description", "Manage connected printing agents.", new() { { "fr", "Gérez les agents d'impression connectés." }, { "en", "Manage connected printing agents." } });
        await CreateTextWithTranslationsAsync("agents.no_agents", "No active agents at the moment.", new() { { "fr", "Aucun agent actif pour le moment." }, { "en", "No active agents at the moment." } });
        await CreateTextWithTranslationsAsync("agents.name", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("agents.type", "Type", new() { { "fr", "Type" }, { "en", "Type" } });
        await CreateTextWithTranslationsAsync("agents.api_key", "API Key", new() { { "fr", "Clé API" }, { "en", "API Key" } });
        await CreateTextWithTranslationsAsync("agents.last_seen", "Last Seen", new() { { "fr", "Dernière vue" }, { "en", "Last Seen" } });
        await CreateTextWithTranslationsAsync("agents.add_title", "Add Agent", new() { { "fr", "Ajouter un agent" }, { "en", "Add Agent" } });
        await CreateTextWithTranslationsAsync("agents.edit_title", "Edit Agent", new() { { "fr", "Modifier l'agent" }, { "en", "Edit Agent" } });

        await CreateTextWithTranslationsAsync("printers.select_protocol", "Select a protocol...", new() { { "fr", "Sélectionner un protocole..." }, { "en", "Select a protocol..." } });
        await CreateTextWithTranslationsAsync("printers.select_resolution", "Select a resolution...", new() { { "fr", "Sélectionner une résolution..." }, { "en", "Select a resolution..." } });
        await CreateTextWithTranslationsAsync("printers.protocol", "Protocol", new() { { "fr", "Protocole" }, { "en", "Protocol" } });
        await CreateTextWithTranslationsAsync("printers.resolution", "Resolution", new() { { "fr", "Résolution" }, { "en", "Resolution" } });
        await CreateTextWithTranslationsAsync("printers.created_at", "Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } });
        await CreateTextWithTranslationsAsync("printers.title", "Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } });
        await CreateTextWithTranslationsAsync("printers.description", "Manage your network printers.", new() { { "fr", "Gérez vos imprimantes réseau." }, { "en", "Manage your network printers." } });
        await CreateTextWithTranslationsAsync("printers.no_printers", "No printers configured.", new() { { "fr", "Aucune imprimante configurée." }, { "en", "No printers configured." } });
        await CreateTextWithTranslationsAsync("printers.add_title", "Add Printer", new() { { "fr", "Ajouter une imprimante" }, { "en", "Add Printer" } });
        await CreateTextWithTranslationsAsync("printers.edit_title", "Edit Printer", new() { { "fr", "Modifier l'imprimante" }, { "en", "Edit Printer" } });
        await CreateTextWithTranslationsAsync("printers.name", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("printers.location", "Location", new() { { "fr", "Emplacement" }, { "en", "Location" } });
        await CreateTextWithTranslationsAsync("printers.ip_port", "IP : Port", new() { { "fr", "IP : Port" }, { "en", "IP : Port" } });
        await CreateTextWithTranslationsAsync("printers.ip", "IP Address", new() { { "fr", "Adresse IP" }, { "en", "IP Address" } });
        await CreateTextWithTranslationsAsync("printers.port", "Port", new() { { "fr", "Port" }, { "en", "Port" } });
        await CreateTextWithTranslationsAsync("printers.status", "Status", new() { { "fr", "Statut" }, { "en", "Status" } });

        await CreateTextWithTranslationsAsync("templates.render.title", "Label Renderer Editor", new() { { "fr", "Éditeur de rendu d'étiquette" }, { "en", "Label Renderer Editor" } });
        await CreateTextWithTranslationsAsync("templates.render.preview", "Preview", new() { { "fr", "Prévisualisation" }, { "en", "Preview" } });
        await CreateTextWithTranslationsAsync("templates.edit_renderer", "Edit Label Renderer", new() { { "fr", "Éditer le rendu de l'étiquette" }, { "en", "Edit Label Renderer" } });
        await CreateTextWithTranslationsAsync("templates.title", "Templates Management", new() { { "fr", "Gestion des modèles" }, { "en", "Templates Management" } });
        await CreateTextWithTranslationsAsync("templates.description", "Manage document and label templates.", new() { { "fr", "Gérez les modèles de documents et d'étiquettes." }, { "en", "Manage document and label templates." } });
        await CreateTextWithTranslationsAsync("templates.no_templates", "No templates available.", new() { { "fr", "Aucun modèle disponible." }, { "en", "No templates available." } });
        await CreateTextWithTranslationsAsync("templates.name", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("templates.status", "Status", new() { { "fr", "Statut" }, { "en", "Status" } });
        await CreateTextWithTranslationsAsync("templates.updated_at", "Updated At", new() { { "fr", "Mis à jour le" }, { "en", "Updated At" } });
        await CreateTextWithTranslationsAsync("templates.add_title", "Add Template", new() { { "fr", "Ajouter un modèle" }, { "en", "Add Template" } });
        await CreateTextWithTranslationsAsync("templates.edit_title", "Edit Template", new() { { "fr", "Modifier le modèle" }, { "en", "Edit Template" } });

        await CreateTextWithTranslationsAsync("nav.home", "Home", new() { { "fr", "Accueil" }, { "en", "Home" } });
        await CreateTextWithTranslationsAsync("nav.templates", "Label Templates", new() { { "fr", "Modèles d'étiquettes" }, { "en", "Label Templates" } });
        await CreateTextWithTranslationsAsync("nav.printers", "Printers", new() { { "fr", "Imprimantes" }, { "en", "Printers" } });
        await CreateTextWithTranslationsAsync("nav.agents", "Agents", new() { { "fr", "Agents" }, { "en", "Agents" } });
        await CreateTextWithTranslationsAsync("nav.users", "Users", new() { { "fr", "Utilisateurs" }, { "en", "Users" } });
        await CreateTextWithTranslationsAsync("nav.history", "History", new() { { "fr", "Historique" }, { "en", "History" } });
        await CreateTextWithTranslationsAsync("nav.my_account", "My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } });
        await CreateTextWithTranslationsAsync("nav.logout", "Logout", new() { { "fr", "Déconnexion" }, { "en", "Logout" } });

        await CreateTextWithTranslationsAsync("account.email_change_warning", "Changing your email will require you to reactivate your account.", new() { { "fr", "Modifier votre e-mail nécessitera une réactivation de votre compte via le lien reçu." }, { "en", "Changing your email will require you to reactivate your account." } });
        await CreateTextWithTranslationsAsync("account.password_section", "Change Password", new() { { "fr", "Modifier le mot de passe" }, { "en", "Change Password" } });
        await CreateTextWithTranslationsAsync("account.new_password_label", "New Password", new() { { "fr", "Nouveau mot de passe" }, { "en", "New Password" } });
        await CreateTextWithTranslationsAsync("account.password_placeholder_leave_blank", "Leave blank to keep current password", new() { { "fr", "Laisser vide pour conserver le mot de passe actuel" }, { "en", "Leave blank to keep current password" } });
        await CreateTextWithTranslationsAsync("account.success.updated", "Account updated successfully", new() { { "fr", "Compte mis à jour avec succès" }, { "en", "Account updated successfully" } });
        await CreateTextWithTranslationsAsync("account.warning.email_changed_inactive", "Your email has changed. Please check your inbox to activate your account.", new() { { "fr", "Votre e-mail a changé. Veuillez vérifier votre boîte de réception pour réactiver votre compte." }, { "en", "Your email has changed. Please check your inbox to activate your account." } });
        await CreateTextWithTranslationsAsync("account.title", "My Account", new() { { "fr", "Mon Compte" }, { "en", "My Account" } });
        await CreateTextWithTranslationsAsync("account.profile_section", "Profile Information", new() { { "fr", "Informations du profil" }, { "en", "Profile Information" } });
        await CreateTextWithTranslationsAsync("account.name_label", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("account.email_label", "Email", new() { { "fr", "E-mail" }, { "en", "Email" } });
        await CreateTextWithTranslationsAsync("account.role_label", "Role", new() { { "fr", "Rôle" }, { "en", "Role" } });
        await CreateTextWithTranslationsAsync("account.subscriptions_title", "My Subscriptions", new() { { "fr", "Mes abonnements" }, { "en", "My Subscriptions" } });
        await CreateTextWithTranslationsAsync("account.no_subscriptions", "No subscriptions found.", new() { { "fr", "Aucun abonnement trouvé." }, { "en", "No subscriptions found." } });
        await CreateTextWithTranslationsAsync("account.subscription.plan", "Plan", new() { { "fr", "Plan" }, { "en", "Plan" } });
        await CreateTextWithTranslationsAsync("account.subscription.type", "Type", new() { { "fr", "Type" }, { "en", "Type" } });
        await CreateTextWithTranslationsAsync("account.subscription.period", "Period", new() { { "fr", "Période" }, { "en", "Period" } });
        await CreateTextWithTranslationsAsync("account.subscription.date", "Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } });

        await CreateTextWithTranslationsAsync("dashboard.title", "Dashboard", new() { { "fr", "Tableau de bord" }, { "en", "Dashboard" } });

        await CreateTextWithTranslationsAsync("error.not_found_title", "Page not found", new() { { "fr", "Page non trouvée" }, { "en", "Page not found" } });
        await CreateTextWithTranslationsAsync("error.not_found_message", "Sorry, the page you are looking for does not exist.", new() { { "fr", "Désolé, la page que vous recherchez n'existe pas." }, { "en", "Sorry, the page you are looking for does not exist." } });

        await CreateTextWithTranslationsAsync("auth.submitting", "Submitting...", new() { { "fr", "En cours..." }, { "en", "Submitting..." } });
        await CreateTextWithTranslationsAsync("auth.processing", "Processing...", new() { { "fr", "Traitement en cours..." }, { "en", "Processing..." } });
        await CreateTextWithTranslationsAsync("auth.success.email_sent", "Recovery email sent successfully.", new() { { "fr", "Email de récupération envoyé avec succès." }, { "en", "Recovery email sent successfully." } });
        await CreateTextWithTranslationsAsync("auth.error.general", "An error occurred.", new() { { "fr", "Une erreur est survenue." }, { "en", "An error occurred." } });
        await CreateTextWithTranslationsAsync("auth.error.unauthorized_title", "Unauthorized access", new() { { "fr", "Accès non autorisé" }, { "en", "Unauthorized access" } });
        await CreateTextWithTranslationsAsync("auth.error.unauthorized_message", "You must log in to access this page.", new() { { "fr", "Vous devez vous connecter pour accéder à cette page." }, { "en", "You must log in to access this page." } });
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
    
        // DTOs Rectrictions
        await CreateTextWithTranslationsAsync("subscription.error.plan_required", "Plan name is required", new() { { "fr", "Le nom du plan est requis" }, { "en", "Plan name is required" } });
        await CreateTextWithTranslationsAsync("subscription.error.type_required", "Subscription type is required", new() { { "fr", "Le type d'abonnement est requis" }, { "en", "Subscription type is required" } });
        await CreateTextWithTranslationsAsync("subscription.error.period_invalid", "Invalid subscription period", new() { { "fr", "Période d'abonnement invalide" }, { "en", "Invalid subscription period" } });

        await CreateTextWithTranslationsAsync("resolutions.error.value_required", "Resolution value is required", new() { { "fr", "La valeur de la résolution est requise" }, { "en", "Resolution value is required" } });
        await CreateTextWithTranslationsAsync("resolutions.error.value_range", "Resolution value must be greater than 0", new() { { "fr", "La valeur de la résolution doit être supérieure à 0" }, { "en", "Resolution value must be greater than 0" } });

        await CreateTextWithTranslationsAsync("protocols.error.name_required", "Protocol name is required", new() { { "fr", "Le nom du protocole est requis" }, { "en", "Protocol name is required" } });

        await CreateTextWithTranslationsAsync("agent.error.name_required", "Agent name is required", new() { { "fr", "Le nom de l'agent est requis" }, { "en", "Agent name is required" } });
        await CreateTextWithTranslationsAsync("agent.error.name_max_length", "Agent name cannot exceed 100 characters", new() { { "fr", "Le nom de l'agent ne peut pas dépasser 100 caractères" }, { "en", "Agent name cannot exceed 100 characters" } });
        await CreateTextWithTranslationsAsync("agent.error.type_required", "Agent type is required", new() { { "fr", "Le type d'agent est requis" }, { "en", "Agent type is required" } });
        await CreateTextWithTranslationsAsync("agent.error.apikey_required", "API key is required", new() { { "fr", "La clé d'API est requise" }, { "en", "API key is required" } });

        await CreateTextWithTranslationsAsync("printhistory.error.printer_name_required", "Printer name is required", new() { { "fr", "Le nom de l'imprimante est requis" }, { "en", "Printer name is required" } });
        await CreateTextWithTranslationsAsync("printhistory.error.template_name_required", "Template name is required", new() { { "fr", "Le nom du modèle est requis" }, { "en", "Template name is required" } });
        await CreateTextWithTranslationsAsync("printhistory.error.input_data_required", "Input data JSON is required", new() { { "fr", "Les données d'entrée JSON sont requises" }, { "en", "Input data JSON is required" } });
        await CreateTextWithTranslationsAsync("printhistory.error.quantity_range", "Quantity must be between 1 and 10000", new() { { "fr", "La quantité doit être comprise entre 1 et 10000" }, { "en", "Quantity must be between 1 and 10000" } });

        await CreateTextWithTranslationsAsync("print.error.apikey_required", "API key is required", new() { { "fr", "La clé d'API est requise" }, { "en", "API key is required" } });
        await CreateTextWithTranslationsAsync("print.error.printer_id_required", "Printer ID is required", new() { { "fr", "L'identifiant de l'imprimante est requis" }, { "en", "Printer ID is required" } });
        await CreateTextWithTranslationsAsync("print.error.printer_id_invalid", "PrinterId must be a valid positive number", new() { { "fr", "L'identifiant de l'imprimante doit être un nombre positif valide" }, { "en", "PrinterId must be a valid positive number" } });
        await CreateTextWithTranslationsAsync("print.error.quantity_required", "Quantity is required", new() { { "fr", "La quantité est requise" }, { "en", "Quantity is required" } });
        await CreateTextWithTranslationsAsync("print.error.quantity_range", "Quantity must be between 1 and 10000", new() { { "fr", "La quantité doit être comprise entre 1 et 10000" }, { "en", "Quantity must be between 1 and 10000" } });

        await CreateTextWithTranslationsAsync("auth.error.password_min_length", "Password must be at least 8 characters long", new() { { "fr", "Le mot de passe doit contenir au moins 8 caractères" }, { "en", "Password must be at least 8 characters long" } });

        await CreateTextWithTranslationsAsync("templates.created_at", "Created At", new() { { "fr", "Créé le" }, { "en", "Created At" } });
        await CreateTextWithTranslationsAsync("templates.json_data", "JSON Data", new() { { "fr", "Données JSON" }, { "en", "JSON Data" } });
        await CreateTextWithTranslationsAsync("template.error.name_required", "Template name is required", new() { { "fr", "Le nom du modèle est requis" }, { "en", "Template name is required" } });
        await CreateTextWithTranslationsAsync("template.error.name_max_length", "Template name cannot exceed 150 characters", new() { { "fr", "Le nom du modèle ne peut pas dépasser 150 caractères" }, { "en", "Template name cannot exceed 150 characters" } });
        await CreateTextWithTranslationsAsync("template.error.json_data_required", "Template JSON data is required", new() { { "fr", "Les données JSON du modèle sont requises" }, { "en", "Template JSON data is required" } });

        await CreateTextWithTranslationsAsync("translation.error.key_required", "Translation key is required", new() { { "fr", "La clé de traduction est requise" }, { "en", "Translation key is required" } });
        await CreateTextWithTranslationsAsync("translation.error.value_required", "Translation value is required", new() { { "fr", "La valeur de traduction est requise" }, { "en", "Translation value is required" } });

        await CreateTextWithTranslationsAsync("user.error.name_required", "User name is required", new() { { "fr", "Le nom d'utilisateur est requis" }, { "en", "User name is required" } });
        await CreateTextWithTranslationsAsync("user.error.name_max_length", "User name cannot exceed 100 characters", new() { { "fr", "Le nom d'utilisateur ne peut pas dépasser 100 caractères" }, { "en", "User name cannot exceed 100 characters" } });

        await CreateTextWithTranslationsAsync("auth.error.name_required", "Name is required", new() { { "fr", "Le nom est requis" }, { "en", "Name is required" } });
        await CreateTextWithTranslationsAsync("auth.error.email_required", "Email is required", new() { { "fr", "L'adresse email est requise" }, { "en", "Email is required" } });
        await CreateTextWithTranslationsAsync("auth.error.email_invalid", "Invalid email address", new() { { "fr", "Adresse email invalide" }, { "en", "Invalid email address" } });
        await CreateTextWithTranslationsAsync("auth.error.password_required", "Password is required", new() { { "fr", "Le mot de passe est requis" }, { "en", "Password is required" } });
        await CreateTextWithTranslationsAsync("auth.error.token_required", "Token is required", new() { { "fr", "Le jeton est requis" }, { "en", "Token is required" } });

        await CreateTextWithTranslationsAsync("printer.error.protocol_required", "Please select a protocol", new() { { "fr", "Veuillez sélectionner un protocole" }, { "en", "Please select a protocol" } });
        await CreateTextWithTranslationsAsync("printer.error.resolution_required", "Please select a resolution", new() { { "fr", "Veuillez sélectionner une résolution" }, { "en", "Please select a resolution" } });
        await CreateTextWithTranslationsAsync("printer.error.name_required", "Printer name is required", new() { { "fr", "Le nom de l'imprimante est requis" }, { "en", "Printer name is required" } });
        await CreateTextWithTranslationsAsync("printer.error.name_max_length", "Printer name cannot exceed 100 characters", new() { { "fr", "Le nom de l'imprimante ne peut pas dépasser 100 caractères" }, { "en", "Printer name cannot exceed 100 characters" } });
        await CreateTextWithTranslationsAsync("printer.error.location_required", "Location is required", new() { { "fr", "L'emplacement est requis" }, { "en", "Location is required" } });
        await CreateTextWithTranslationsAsync("printer.error.ip_required", "IP address is required", new() { { "fr", "L'adresse IP est requise" }, { "en", "IP address is required" } });
        await CreateTextWithTranslationsAsync("printer.error.port_invalid", "Port must be between 1 and 65535", new() { { "fr", "Le port doit être compris entre 1 et 65535" }, { "en", "Port must be between 1 and 65535" } });

        await CreateTextWithTranslationsAsync("label.error.name_required", "Label name is required", new() { { "fr", "Le nom de l'étiquette est requis" }, { "en", "Label name is required" } });
        await CreateTextWithTranslationsAsync("label.error.name_max_length", "Label name cannot exceed 150 characters", new() { { "fr", "Le nom de l'étiquette ne peut pas dépasser 150 caractères" }, { "en", "Label name cannot exceed 150 characters" } });
        await CreateTextWithTranslationsAsync("label.error.content_required", "Label content is required", new() { { "fr", "Le contenu de l'étiquette est requis" }, { "en", "Label content is required" } });
        await CreateTextWithTranslationsAsync("label.error.description_max_length", "Description cannot exceed 500 characters", new() { { "fr", "La description ne peut pas dépasser 500 caractères" }, { "en", "Description cannot exceed 500 characters" } });
    
        // Endpoints
        await CreateTextWithTranslationsAsync("subscription.error.unauthorized", "Unauthorized access", new() { { "fr", "Accès non autorisé" }, { "en", "Unauthorized access" } });

        await CreateTextWithTranslationsAsync("user.error.email_already_taken", "This email is already in use", new() { { "fr", "Cet e-mail est déjà utilisé" }, { "en", "This email is already in use" } });
        await CreateTextWithTranslationsAsync("user.error.not_found", "User not found", new() { { "fr", "Utilisateur introuvable" }, { "en", "User not found" } });

        await CreateTextWithTranslationsAsync("template.error.not_found", "Label template not found", new() { { "fr", "Modèle d'étiquette introuvable" }, { "en", "Label template not found" } });
        await CreateTextWithTranslationsAsync("templates.title", "Templates", new() { { "fr", "Modèles" }, { "en", "Templates" } });
        await CreateTextWithTranslationsAsync("templates.description", "Manage your label printing templates.", new() { { "fr", "Gérez vos modèles d'impression d'étiquettes." }, { "en", "Manage your label printing templates." } });
        await CreateTextWithTranslationsAsync("templates.no_templates", "No templates found.", new() { { "fr", "Aucun modèle trouvé." }, { "en", "No templates found." } });
        await CreateTextWithTranslationsAsync("templates.add_title", "Add Template", new() { { "fr", "Ajouter un modèle" }, { "en", "Add Template" } });
        await CreateTextWithTranslationsAsync("templates.edit_title", "Edit Template", new() { { "fr", "Modifier le modèle" }, { "en", "Edit Template" } });
        await CreateTextWithTranslationsAsync("templates.name", "Name", new() { { "fr", "Nom" }, { "en", "Name" } });
        await CreateTextWithTranslationsAsync("templates.status", "Status", new() { { "fr", "Statut" }, { "en", "Status" } });
        await CreateTextWithTranslationsAsync("templates.updated_at", "Updated At", new() { { "fr", "Mis à jour le" }, { "en", "Updated At" } });
        await CreateTextWithTranslationsAsync("template.error.name_required", "Template name is required", new() { { "fr", "Le nom du modèle est requis" }, { "en", "Template name is required" } });
        await CreateTextWithTranslationsAsync("template.error.name_max_length", "Template name cannot exceed 150 characters", new() { { "fr", "Le nom du modèle ne peut pas dépasser 150 caractères" }, { "en", "Template name cannot exceed 150 characters" } });
        await CreateTextWithTranslationsAsync("template.error.json_data_required", "JSON data is required", new() { { "fr", "Les données JSON sont requises" }, { "en", "JSON data is required" } });
        await CreateTextWithTranslationsAsync("template.error.not_found", "Template not found", new() { { "fr", "Modèle introuvable" }, { "en", "Template not found" } });

        await CreateTextWithTranslationsAsync("job.error.invalid_limit", "Limit must be greater than zero", new() { { "fr", "La limite doit être supérieure à zéro" }, { "en", "Limit must be greater than zero" } });
        await CreateTextWithTranslationsAsync("job.error.not_found", "Print job not found", new() { { "fr", "Tâche d'impression introuvable" }, { "en", "Print job not found" } });
        
        await CreateTextWithTranslationsAsync("printer.error.invalid_protocol", "Invalid protocol selected", new() { { "fr", "Protocole sélectionné invalide" }, { "en", "Invalid protocol selected" } });
        await CreateTextWithTranslationsAsync("printer.error.invalid_resolution", "Invalid resolution selected", new() { { "fr", "Résolution sélectionnée invalide" }, { "en", "Invalid resolution selected" } });
        await CreateTextWithTranslationsAsync("printer.error.not_found", "Printer not found", new() { { "fr", "Imprimante introuvable" }, { "en", "Printer not found" } });

        await CreateTextWithTranslationsAsync("language.error.invalid", "Unsupported language", new() { { "fr", "Langue non supportée" }, { "en", "Unsupported language" } });

        await CreateTextWithTranslationsAsync("print.error.agent_not_connected", "The print agent is not connected", new() { { "fr", "L'agent d'impression n'est pas connecté" }, { "en", "The print agent is not connected" } });
        await CreateTextWithTranslationsAsync("print.error.template_not_found", "Template not found", new() { { "fr", "Modèle introuvable" }, { "en", "Template not found" } });
        await CreateTextWithTranslationsAsync("print.error.timeout", "Timeout: The local agent did not respond", new() { { "fr", "Délai dépassé : L'agent local n'a pas répondu" }, { "en", "Timeout: The local agent did not respond" } });

        await CreateTextWithTranslationsAsync("auth.error.account_inactive", "Account is inactive", new() { { "fr", "Votre compte est inactif" }, { "en", "Account is inactive" } });
        await CreateTextWithTranslationsAsync("auth.error.invalid_token", "Invalid token", new() { { "fr", "Jeton invalide" }, { "en", "Invalid token" } });

        await CreateTextWithTranslationsAsync("agent.error.not_found", "Agent not found", new() { { "fr", "Agent introuvable" }, { "en", "Agent not found" } });
    }
}