namespace FastLabel.Backend.Data;

public class DevelopmentDataSeeder(AppDbContext context, IPasswordHasher<User> passwordHasher)
{
    private readonly AppDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    public async Task SeedAsync()
    {
        // Trouve le statut actif
        var statusTextId = await _context.Texts
            .Where(t => t.Tag == "status.active")
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Le texte avec le tag 'status.active' est introuvable dans la base. Le DbInitializer a-t-il bien été exécuté avant ?");

        var activeStatus = await _context.BasicStatuses
            .FirstOrDefaultAsync(s => s.NameTextId == statusTextId) ?? throw new InvalidOperationException("Le BasicStatus correspondant au tag 'status.active' est introuvable.");

        int activeStatusId = activeStatus.Id;

        // 1. Création des utilisateurs par défaut (Regular, Reseller)
        if (!await _context.Users.AnyAsync())
        {
            // Trouve la langue anglaise
            var englishLanguage = await _context.Languages
                .FirstOrDefaultAsync(l => l.Code == "en") ?? throw new InvalidOperationException("La langue anglaise ne peut pas être trouvée");
            int englishLanguageId = englishLanguage.Id;

            var adminUser = new User
            {
                Name = "Test Admin",
                Email = "admintest@yopmail.fr",
                BasicStatusId = activeStatusId,
                LanguageId = englishLanguageId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true,
                IsReseller = false
            };
            adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, "12345678");

            var regularUser = new User
            {
                Name = "Test User",
                Email = "usertest@yopmail.fr",
                BasicStatusId = activeStatusId,
                LanguageId = englishLanguageId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false,
                IsReseller = false
            };
            regularUser.PasswordHash = _passwordHasher.HashPassword(regularUser, "12345678");

            var resellerUser = new User
            {
                Name = "Test Reseller",
                Email = "resellertest@yopmail.fr",
                BasicStatusId = activeStatusId,
                LanguageId = englishLanguageId,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false,
                IsReseller = true,
            };
            resellerUser.PasswordHash = _passwordHasher.HashPassword(resellerUser, "12345678");

            _context.Users.AddRange(adminUser, regularUser, resellerUser);
            await _context.SaveChangesAsync();
        }

        // 2. Agent Windows par défaut pour chaque utilisateur
        if (!await _context.Agents.AnyAsync())
        {
            var textId = await _context.Texts
                .Where(t => t.Tag == "agenttype.windows")
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Le texte de l'agent windows n'a pas été trouvé");

            var windowsAgentType = await _context.AgentTypes
                .FirstOrDefaultAsync(at => at.NameTextId == textId) ?? throw new InvalidOperationException("L'agent windows n'a pas été trouvé");

            int windowsAgentTypeId = windowsAgentType.Id;

            textId = await _context.Texts
                .Where(t => t.Tag == "agenttype.linux")
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Le texte de l'agent windows n'a pas été trouvé");

            var linuxAgentType = await _context.AgentTypes
                .FirstOrDefaultAsync(at => at.NameTextId == textId) ?? throw new InvalidOperationException("L'agent linux n'a pas été trouvé");

            int linuxAgentTypeId = linuxAgentType.Id;

            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                var windowsAgent = new Agent
                {
                    UserId = user.Id,
                    AgentTypeId = windowsAgentTypeId,
                    ApiKey = $"fl_{Guid.NewGuid():N}{Guid.NewGuid():N}",
                    CreatedAt = DateTime.UtcNow
                };

                var linuxAgent = new Agent
                {
                    UserId = user.Id,
                    AgentTypeId = linuxAgentTypeId,
                    ApiKey = $"fl_{Guid.NewGuid():N}{Guid.NewGuid():N}",
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Agents.AddRange(windowsAgent, linuxAgent);
            }
            await _context.SaveChangesAsync();
        }

        // 3. Imprimante par défaut ("Test Printer") pour chaque utilisateur
        if (!await _context.Printers.AnyAsync())
        {
            var protoTextId = await _context.Texts
                .Where(t => t.Tag == "protocol.rasterized_zpl")
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync() ?? throw new InvalidOperationException("Le texte du protocole ZPL rastérisé n'a pas été trouvé");

            var protocol = await _context.Protocols
                .FirstOrDefaultAsync(p => p.NameTextId == protoTextId) ?? throw new InvalidOperationException("Le protocole ZPL rastérisé n'a pas été trouvé");

            int protocolId = protocol.Id;

            var resolution = await _context.ResolutionDpis
                .FirstOrDefaultAsync(r => r.Value == 300) ?? throw new InvalidOperationException("La résolution 300 n'a pas été trouvée");

            int resolutionId = resolution.Id;

            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                for (int i = 1; i <= 100; i++)
                {
                    _context.Printers.Add(new Printer
                    {
                        UserId = user.Id,
                        Name = $"Test Printer #{i}",
                        Location = "Room 1",
                        IP = "192.168.1.25",
                        Port = 9100,
                        ProtocolId = protocolId,
                        ResolutionDpiId = resolutionId,
                        BasicStatusId = activeStatusId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // 4. Modèle d'étiquette par défaut ("Label Test") pour chaque utilisateur
        if (!await _context.Templates.AnyAsync())
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                for (int i = 1; i <= 100; i++)
                {
                    _context.Templates.Add(new Template
                    {
                        UserId = user.Id,
                        Name = $"Test Label #{i}",
                        BasicStatusId = activeStatusId,
                        JsonData = "{}",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}