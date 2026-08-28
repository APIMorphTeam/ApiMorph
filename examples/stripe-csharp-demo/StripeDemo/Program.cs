using StripeDemo.Services;

var paymentService = new PaymentService();
Console.WriteLine("Stripe demo app — intentionally contains outdated Stripe.net patterns for ApiMorph tests.");
await paymentService.CreateChargeAsync("tok_visa", 2000);
await paymentService.RefundChargeAsync("ch_test", 500);
