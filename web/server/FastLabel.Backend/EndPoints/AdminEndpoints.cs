namespace FastLabel.Backend.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var adminGroup = routes.MapGroup("/api/admin").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/db/status", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            if (!canConnect)
            {
                return Results.Json(
                    new ErrorResponseDto { Message = "admin.db.error.connection_failed" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var nowUtc = DateTime.UtcNow;
            var last24h = nowUtc.AddHours(-24);
            var last7d = nowUtc.AddDays(-7);
            var last30d = nowUtc.AddDays(-30);

            var usersCount = await DbHelpers.GetUsersCountAsync(db, cancellationToken);
            var resellersCount = await DbHelpers.GetResellersCountAsync(db, cancellationToken);
            var subscriptionsCount = await DbHelpers.GetSubscriptionsCountAsync(db, cancellationToken);
            var agentsCount = await DbHelpers.GetAgentsCountAsync(db, cancellationToken);
            var activeAgentsLast24h = await DbHelpers.GetActiveAgentsCountAsync(db, last24h, cancellationToken);
            var templatesCount = await DbHelpers.GetTemplatesCountAsync(db, cancellationToken);
            var printersCount = await DbHelpers.GetPrintersCountAsync(db, cancellationToken);
            var printJobs24h = await DbHelpers.GetPrintJobsCountSinceAsync(db, last24h, cancellationToken);
            var printJobs7d = await DbHelpers.GetPrintJobsCountSinceAsync(db, last7d, cancellationToken);
            var printJobs30d = await DbHelpers.GetPrintJobsCountSinceAsync(db, last30d, cancellationToken);
            var printedLabels24h = await DbHelpers.GetPrintedLabelsQuantitySinceAsync(db, last24h, cancellationToken);
            var printedLabels7d = await DbHelpers.GetPrintedLabelsQuantitySinceAsync(db, last7d, cancellationToken);
            var printedLabels30d = await DbHelpers.GetPrintedLabelsQuantitySinceAsync(db, last30d, cancellationToken);

            return Results.Ok(new
            {
                Connected = true,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                UsersCount = usersCount,
                ResellersCount = resellersCount,
                SubscriptionsCount = subscriptionsCount,
                AgentsCount = agentsCount,
                ActiveAgentsLast24h = activeAgentsLast24h,
                TemplatesCount = templatesCount,
                PrintersCount = printersCount,
                PrintJobs24h = printJobs24h,
                PrintJobs7d = printJobs7d,
                PrintJobs30d = printJobs30d,
                PrintedLabels24h = printedLabels24h,
                PrintedLabels7d = printedLabels7d,
                PrintedLabels30d = printedLabels30d
            });
        });

        adminGroup.MapGet("/stripe/status", async (IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var apiKey = Stripe.StripeConfiguration.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.stripe.error.key_missing" });
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var productService = new Stripe.ProductService();
                await productService.ListAsync(new Stripe.ProductListOptions { Limit = 1 }, cancellationToken: cancellationToken);
                stopwatch.Stop();

                return Results.Ok(new
                {
                    Connected = true,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    IsTestMode = apiKey.StartsWith("sk_test_", StringComparison.Ordinal),
                    HasWebhookSecret = !string.IsNullOrWhiteSpace(configuration["STRIPE_WEBHOOK_SECRET"])
                });
            }
            catch (Stripe.StripeException ex)
            {
                stopwatch.Stop();
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.stripe.error.connection_failed" });
            }
        });

        adminGroup.MapPost("/smtp/test-connection", async (
            IConfiguration configuration,
            ClaimsPrincipal claimsUser,
            CancellationToken cancellationToken) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            if (userId == 0)
            {
                return Results.Unauthorized();
            }

            var smtpServer = configuration["SMTP_SERVER"];
            var portStr = configuration["SMTP_PORT"];
            var username = configuration["SMTP_USER_NAME"];
            var password = configuration["SMTP_PASSWORD"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(portStr) ||
                !int.TryParse(portStr, out int port) ||
                port is < 1 or > 65535 ||
                (string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password)))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.smtp.error.connection_failed" });
            }

            var stopwatch = Stopwatch.StartNew();
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                await smtp.ConnectAsync(smtpServer, port, SecureSocketOptions.Auto, cancellationToken);

                if (!string.IsNullOrEmpty(username))
                {
                    await smtp.AuthenticateAsync(username, password, cancellationToken);
                }

                await smtp.NoOpAsync(cancellationToken);
                stopwatch.Stop();

                return Results.Ok(new
                {
                    Connected = true,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.smtp.error.connection_failed" });
            }
            finally
            {
                if (smtp.IsConnected)
                {
                    await smtp.DisconnectAsync(true, cancellationToken);
                }
            }
        });

        adminGroup.MapPost("/smtp/test-email", async (
            [FromQuery] string? email,
            IEmailService emailService,
            AppDbContext db,
            HttpContext httpContext,
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            if (userId == 0)
            {
                return Results.Unauthorized();
            }

            var targetEmail = email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.smtp.error.email_required" });
            }

            if (!MailAddress.TryCreate(targetEmail, out _))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.smtp.error.email_invalid" });
            }

            var (_, languageCode) = await DbHelpers.GetUserLanguageByUserClaimAsync(httpContext, db, claimsUser);

            var texts = await DbHelpers.GetLocalizedTextsAsync(
                db,
                languageCode,
                "admin.smtp.test.subject",
                "admin.smtp.test.body");

            var subject = texts.GetValueOrDefault("admin.smtp.test.subject", "admin.smtp.test.subject");
            var body = texts.GetValueOrDefault("admin.smtp.test.body", "admin.smtp.test.body");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await emailService.SendEmailAsync(targetEmail, subject, body);
                stopwatch.Stop();

                return Results.Ok(new
                {
                    Sent = true,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "admin.smtp.error.send_failed" });
            }
        });
    }
}