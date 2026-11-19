namespace FastLabel.Backend.Data;

public class DevelopmentDataSeeder(AppDbContext context, IPasswordHasher<User> passwordHasher)
{
    private readonly AppDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;

    public async Task SeedAsync()
    {
        // 1. Création des utilisateurs par défaut (Regular, Reseller)
        if (!await _context.Users.AnyAsync())
        {
            int statusId = await DbHelpers.GetStatusIdByTagAsync(_context, "status.active");
            var language = await DbHelpers.GetLanguageByCodeAsync(_context, "en")?? throw new InvalidOperationException("La langue anglaise ne peut pas être trouvée");

            var adminUser = new User
            {
                Name = "Test Admin",
                Email = "admintest@yopmail.fr",
                BasicStatusId = statusId,
                LanguageId = language.Id,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = true,
                IsReseller = false
            };
            adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, "12345678");

            var regularUser = new User
            {
                Name = "Test User",
                Email = "usertest@yopmail.fr",
                BasicStatusId = statusId,
                LanguageId = language.Id,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false,
                IsReseller = false
            };
            regularUser.PasswordHash = _passwordHasher.HashPassword(regularUser, "12345678");

            var resellerUser = new User
            {
                Name = "Test Reseller",
                Email = "resellertest@yopmail.fr",
                BasicStatusId = statusId,
                LanguageId = language.Id,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false,
                IsReseller = true,
            };
            resellerUser.PasswordHash = _passwordHasher.HashPassword(resellerUser, "12345678");

            _context.Users.AddRange(adminUser, regularUser, resellerUser);
            await _context.SaveChangesAsync();
        }

        // 2. Souscription 'free' par défaut pour chaque utilisateur
        if (!await _context.Subscriptions.AnyAsync())
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                var freeSubscription = await DbHelpers.CreateSubscription(_context, user.Id, "free", 0, null, DateTime.UtcNow, null, 0);        
                _context.Subscriptions.AddRange(freeSubscription);
            }

            await _context.SaveChangesAsync();
        }

        // 3. Agent Windows par défaut pour chaque utilisateur
        if (!await _context.Agents.AnyAsync())
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                var windowsAgent = await DbHelpers.CreateAgent(_context, user.Id, "agenttype.windows");
                var linuxAgent = await DbHelpers.CreateAgent(_context, user.Id, "agenttype.linux");
                
                _context.Agents.AddRange(windowsAgent, linuxAgent);
            }
            await _context.SaveChangesAsync();
        }

        // 4. Imprimante par défaut ("Test Printer") pour chaque utilisateur
        if (!await _context.Printers.AnyAsync())
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                for (int i = 1; i <= 100; i++)
                {
                    var testprinter = await DbHelpers.CreatePrinter(_context, user.Id, $"Test Printer #{i}", $"Room {i}", "192.168.1.25", 9100, "protocol.rasterized_zpl", 300);
                    _context.Printers.Add(testprinter);
                }
            }
            await _context.SaveChangesAsync();
        }

        // 5. Modèle d'étiquette par défaut ("Label Test") pour chaque utilisateur
        if (!await _context.Templates.AnyAsync())
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                for (int i = 1; i <= 100; i++)
                {
                    var testprinter = await DbHelpers.CreateTemplate(_context, user.Id, $"Test Label #{i}");
                    _context.Templates.Add(testprinter);
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}