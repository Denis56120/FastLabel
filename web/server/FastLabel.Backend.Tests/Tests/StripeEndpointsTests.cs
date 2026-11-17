namespace FastLabel.Backend.Tests.Tests;

public class StripeEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private HttpClient CreateClientWithMockedStripe()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remplace le service Stripe réel par une version de test factice
                services.AddScoped<IStripeService, FakeStripeService>();
            });
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stripe:ProMonthly"] = "price_fake_monthly_123"
                });
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private class FakeStripeService : IStripeService
    {
        public Task<string> CreateCustomerAsync(string email, string name)
        {
            return Task.FromResult("cus_fake_123");
        }

        public Task<string> CreateSubscriptionAsync(string customerId, string priceId, int initialQuantity = 1)
        {
            return Task.FromResult("sub_fake_123");
        }

        public Task IncrementSubscriptionQuantityAsync(string subscriptionId) => Task.CompletedTask;

        public Task DecrementSubscriptionQuantityAsync(string subscriptionId) => Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WithInvalidInterval_ReturnsBadRequest()
    {
        var client = CreateClientWithMockedStripe();
        var dto = new StripeDto
        {
            Plan = "Pro",
            BillingInterval = "Daily" // Invalide
        };

        var response = await client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.invalid_interval", error?.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WhenPlanNotFound_ReturnsBadRequest()
    {
        var client = CreateClientWithMockedStripe();
        var dto = new StripeDto
        {
            Plan = "InexistantPlan",
            BillingInterval = "Monthly"
        };

        var response = await client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.plan_not_found", error?.Message);
    }
}