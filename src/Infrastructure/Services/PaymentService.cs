using Core.Interfaces;
using Core.Enums;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<PaymentService> _logger;

        // Simulated pricing - in real app, this would come from configuration
        private readonly Dictionary<FeatureType, decimal> _monthlyPrices = new()
        {
            [FeatureType.FileSharing] = 4.99m,
            [FeatureType.VoiceMessage] = 2.99m,
            [FeatureType.GroupChat] = 9.99m,
            [FeatureType.EmailAlerts] = 1.99m
        };

        public PaymentService(ISubscriptionService subscriptionService, ILogger<PaymentService> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
        {
            _logger.LogInformation("Processing payment for user {UserId}, feature {Feature}, {Months} months",
                request.UserId, request.Feature, request.Months);

            // Simulate payment gateway call
            await Task.Delay(500);

            var paymentSuccessful = await SimulatePaymentGateway(request.PaymentToken, request.Amount);

            if (paymentSuccessful)
            {
                await _subscriptionService.GrantAsync(request.UserId, request.Feature, TimeSpan.FromDays(30 * request.Months));

                _logger.LogInformation("Payment successful for user {UserId}, granted {Feature} for {Months} months",
                    request.UserId, request.Feature, request.Months);

                return new PaymentResponse
                {
                    Success = true,
                    TransactionId = $"TXN_{Guid.NewGuid()}",
                    Message = "Payment processed successfully",
                    ProcessedAt = DateTime.UtcNow
                };
            }

            _logger.LogWarning("Payment failed for user {UserId}, feature {Feature}", request.UserId, request.Feature);
            return new PaymentResponse
            {
                Success = false,
                TransactionId = string.Empty,
                Message = "Payment processing failed",
                ProcessedAt = DateTime.UtcNow
            };
        }

        public decimal CalculateAmount(FeatureType feature, int months)
        {
            return _monthlyPrices.ContainsKey(feature) ? _monthlyPrices[feature] * months : 0m;
        }

        public async Task<List<FeaturePlan>> GetAvailablePlansAsync()
        {
            return await Task.FromResult(new List<FeaturePlan>
            {
                new FeaturePlan { Feature = FeatureType.VoiceMessage, MonthlyPrice = 2.99m, Description = "Send voice messages" },
                new FeaturePlan { Feature = FeatureType.FileSharing, MonthlyPrice = 4.99m, Description = "Share files in messages" },
                new FeaturePlan { Feature = FeatureType.GroupChat, MonthlyPrice = 9.99m, Description = "Create and join group chats" },
                new FeaturePlan { Feature = FeatureType.EmailAlerts, MonthlyPrice = 1.99m, Description = "Smart email notifications" }
            });
        }

        private async Task<bool> SimulatePaymentGateway(string paymentToken, decimal amount)
        {
            await Task.Delay(100);

            if (paymentToken == "payment_failed" || string.IsNullOrEmpty(paymentToken))
                return false;

            return true;
        }
    }
}