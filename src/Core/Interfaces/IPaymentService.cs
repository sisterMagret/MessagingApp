using Core.Enums;

namespace Core.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
        decimal CalculateAmount(FeatureType feature, int months);
        Task<List<FeaturePlan>> GetAvailablePlansAsync();
    }

    public class PaymentRequest
    {
        public int UserId { get; set; }
        public FeatureType Feature { get; set; }
        public int Months { get; set; }
        public string PaymentToken { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class PaymentResponse
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    public class FeaturePlan
    {
        public FeatureType Feature { get; set; }
        public decimal MonthlyPrice { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}