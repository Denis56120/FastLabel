using Stripe;

namespace FastLabel.Backend.Services;

public class StripeService
{
    private readonly IConfiguration _configuration;

    public StripeService(IConfiguration configuration)
    {
        _configuration = configuration;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateCustomerAsync(string email, string name)
    {
        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = name
        };

        var service = new CustomerService();
        var customer = await service.CreateAsync(options);
        return customer.Id;
    }

    public async Task<string> CreateSubscriptionAsync(string customerId, string priceId, int initialQuantity = 1)
    {
        var options = new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = new List<SubscriptionItemOptions>
            {
                new SubscriptionItemOptions
                {
                    Price = priceId,
                    Quantity = initialQuantity
                }
            },
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new SubscriptionPaymentSettingsOptions
            {
                SaveDefaultPaymentMethod = "on_subscription",
            },
            Expand = new List<string> { "latest_invoice.payment_intent" },
        };

        var service = new SubscriptionService();
        var subscription = await service.CreateAsync(options);
        return subscription.Id;
    }

    public async Task IncrementSubscriptionQuantityAsync(string subscriptionId)
    {
        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);
        
        var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        if (subscriptionItem == null)
            throw new Exception("Aucun élément d'abonnement Stripe trouvé.");

        var options = new SubscriptionItemUpdateOptions
        {
            Quantity = subscriptionItem.Quantity + 1
        };

        var itemService = new SubscriptionItemService();
        await itemService.UpdateAsync(subscriptionItem.Id, options);
    }

    public async Task DecrementSubscriptionQuantityAsync(string subscriptionId)
    {
        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);
        
        var subscriptionItem = subscription.Items.Data.FirstOrDefault();
        if (subscriptionItem == null)
            throw new Exception("Aucun élément d'abonnement Stripe trouvé.");

        if (subscriptionItem.Quantity <= 1)
            return;

        var options = new SubscriptionItemUpdateOptions
        {
            Quantity = subscriptionItem.Quantity - 1
        };

        var itemService = new SubscriptionItemService();
        await itemService.UpdateAsync(subscriptionItem.Id, options);
    }
}