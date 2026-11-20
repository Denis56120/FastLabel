namespace FastLabel.Backend.Services;

public interface IStripeService
{
    Task<string> CreateCustomerAsync(string email, string name);
    Task<string> CreateSubscriptionAsync(string customerId, string priceId, int initialQuantity = 1);
    Task IncrementSubscriptionQuantityAsync(string subscriptionId);
    Task DecrementSubscriptionQuantityAsync(string subscriptionId);
}

public class StripeService : IStripeService
{
    public static void SetApiKey(IConfiguration configuration)
    {
        var apiKey = ConfigurationHelpers.GetStripeKey(configuration);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("stripe.error.api_key_missing");
        Stripe.StripeConfiguration.ApiKey = apiKey;
    }

    public async Task<string> CreateCustomerAsync(string email, string name)
    {
        var options = new Stripe.CustomerCreateOptions
        {
            Email = email,
            Name = name
        };

        var service = new Stripe.CustomerService();
        var customer = await service.CreateAsync(options);
        return customer.Id;
    }

    public async Task<string> CreateSubscriptionAsync(string customerId, string priceId, int initialQuantity = 1)
    {
        var options = new Stripe.SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = new List<Stripe.SubscriptionItemOptions>
            {
                new Stripe.SubscriptionItemOptions
                {
                    Price = priceId,
                    Quantity = initialQuantity
                }
            },
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new Stripe.SubscriptionPaymentSettingsOptions
            {
                SaveDefaultPaymentMethod = "on_subscription",
            },
            Expand = new List<string> { "latest_invoice.payment_intent" },
        };

        var service = new Stripe.SubscriptionService();
        var subscription = await service.CreateAsync(options);
        return subscription.Id;
    }

    public async Task IncrementSubscriptionQuantityAsync(string subscriptionId)
    {
        var subscriptionService = new Stripe.SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);

        var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        if (subscriptionItem == null)
            throw new Exception("Aucun élément d'abonnement Stripe trouvé.");

        var options = new Stripe.SubscriptionItemUpdateOptions
        {
            Quantity = subscriptionItem.Quantity + 1
        };

        var itemService = new Stripe.SubscriptionItemService();
        await itemService.UpdateAsync(subscriptionItem.Id, options);
    }

    public async Task DecrementSubscriptionQuantityAsync(string subscriptionId)
    {
        var subscriptionService = new Stripe.SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);

        var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        if (subscriptionItem == null)
            throw new Exception("Aucun élément d'abonnement Stripe trouvé.");

        if (subscriptionItem.Quantity <= 1)
            return;

        var options = new Stripe.SubscriptionItemUpdateOptions
        {
            Quantity = subscriptionItem.Quantity - 1
        };

        var itemService = new Stripe.SubscriptionItemService();
        await itemService.UpdateAsync(subscriptionItem.Id, options);
    }
}