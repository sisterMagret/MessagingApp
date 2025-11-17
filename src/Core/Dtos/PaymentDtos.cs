using Core.Enums;

namespace Core.Dtos
{
    public class PaymentRequest
    {
        public int UserId { get; set; }
        public FeatureType Feature { get; set; }
        public int DurationMonths { get; set; } = 1;
        public string PaymentMethodId { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CVV { get; set; } = string.Empty;
        public string CardHolderName { get; set; } = string.Empty;
    }

    public class PaymentResponse
    {
        public string PaymentId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpiresAt { get; set; }
        public SubscriptionDto? Subscription { get; set; }
    }

    public class SubscriptionDto
    {
        public int Id { get; set; }
        public FeatureType Feature { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public string FeatureName => Feature.ToString();
    }

    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    public class SubscriptionPlan
    {
        public FeatureType Feature { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int MinimumMonths { get; set; } = 1;
        public int MaximumMonths { get; set; } = 12;
        public List<string> Features { get; set; } = new();
    }

    public class UserSubscriptionSummary
    {
        public int UserId { get; set; }
        public List<SubscriptionDto> ActiveSubscriptions { get; set; } = new();
        public List<SubscriptionDto> ExpiredSubscriptions { get; set; } = new();
        public decimal MonthlyCost { get; set; }
    }
}