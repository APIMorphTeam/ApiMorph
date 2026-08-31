namespace StripeDemo.Services;

using Stripe;

public sealed class PaymentService
{
    public async Task CreateChargeAsync(string token, long amountCents)
    {
        StripeConfiguration.ApiVersion = "2019-12-03";

        var chargeService = new ChargeService();
        await chargeService.CreateAsync(new ChargeCreateOptions
        {
            Amount = amountCents,
            Currency = "usd",
            Source = token,
        });
    }

    public async Task RefundChargeAsync(string chargeId, long amountCents)
    {
        var refundService = new RefundService();
        await refundService.CreateAsync(new RefundCreateOptions
        {
            Charge = chargeId,
            Amount = amountCents,
        });
    }
}
