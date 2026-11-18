namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class StripeEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

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
        var dto = new StripeDto
        {
            Plan = "Pro",
            BillingInterval = "Daily" // Invalide
        };

        var response = await _client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.invalid_interval", error?.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WhenPlanNotFound_ReturnsBadRequest()
    {
        var dto = new StripeDto
        {
            Plan = "InexistantPlan",
            BillingInterval = "Monthly"
        };

        var response = await _client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.plan_not_found", error?.Message);
    }
}