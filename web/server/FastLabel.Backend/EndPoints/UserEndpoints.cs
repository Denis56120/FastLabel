using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using FastLabel.Backend.Models;

namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users/me", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var userDto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == userId)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (userDto == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            return Results.Ok(userDto);
        }).RequireAuthorization();

        routes.MapPut("/api/users/me", async (UserDto updatedDto, AppDbContext db, IPasswordHasher<User> passwordHasher, IEmailService emailService, IConfiguration configuration, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            bool emailChanged = !string.Equals(user.Email, updatedDto.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                bool emailExists = await db.Users.AnyAsync(u => u.Email == updatedDto.Email && u.Id != userId);
                if (emailExists)
                {
                    return Results.BadRequest(new ErrorResponseDto { Message = "user.error.email_already_taken" });
                }

                user.Email = updatedDto.Email;
                user.EmailVerificationToken = Guid.NewGuid().ToString();

                var inactiveStatus = await db.BasicStatuses
                    .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                    .Where(x => x.Tag == "status.inactive")
                    .OrderBy(x => x.bs.Id)
                    .Select(x => x.bs)
                    .FirstOrDefaultAsync();

                if (inactiveStatus != null)
                {
                    user.BasicStatusId = inactiveStatus.Id;
                }
                else
                {
                    var fallbackStatus = await db.BasicStatuses.OrderBy(s => s.Id).Skip(1).FirstOrDefaultAsync();
                    if (fallbackStatus != null)
                    {
                        user.BasicStatusId = fallbackStatus.Id;
                    }
                }

                // Envoi de l'e-mail d'activation avec le nouveau token
                var serverUrl = configuration["Url:ServerUrl"] ?? "https://fastlabel.com";
                var verificationLink = $"{serverUrl}/api/auth/verify-email?token={user.EmailVerificationToken}";

                var languageCode = httpContext.GetRequestLanguage();

                var emailTexts = await db.Texts
                    .Where(t => t.Tag == "email.activation.subject" || t.Tag == "email.activation.body")
                    .Select(t => new
                    {
                        t.Tag,
                        Value = t.Translations
                            .Where(tr => tr.Language.Code == languageCode)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() ?? t.DefaultTranslation
                    })
                    .ToListAsync();

                var subject = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.subject")?.Value
                            ?? string.Empty;

                var bodyTemplate = emailTexts.FirstOrDefault(t => t.Tag == "email.activation.body")?.Value
                                            ?? string.Empty;

                var message = string.Format(bodyTemplate, updatedDto.Name, verificationLink);

                await emailService.SendEmailAsync(user.Email, subject, message);
            }

            user.Name = updatedDto.Name;

            if (!string.IsNullOrWhiteSpace(updatedDto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, updatedDto.Password);
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { emailChanged });
        }).RequireAuthorization();

        var adminGroup = routes.MapGroup("/api/users").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async (AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var users = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(users);
        });

        adminGroup.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var dto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == id)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (dto == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            return Results.Ok(dto);
        });

        adminGroup.MapPost("/", async (UserDto dto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                IsAdmin = dto.IsAdmin,
                BasicStatusId = 1,
                CreatedAt = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
            }

            db.Users.Add(user);
            await db.SaveChangesAsync();

            dto.Id = user.Id;
            dto.Password = null; 
            return Results.Created($"/api/users/{user.Id}", dto);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UserDto updatedDto, AppDbContext db, IPasswordHasher<User> passwordHasher) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            user.Name = updatedDto.Name;
            user.Email = updatedDto.Email;
            user.IsAdmin = updatedDto.IsAdmin;

            if (!string.IsNullOrWhiteSpace(updatedDto.Password))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, updatedDto.Password);
            }

            await db.SaveChangesAsync();
            return Results.Ok(user);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}