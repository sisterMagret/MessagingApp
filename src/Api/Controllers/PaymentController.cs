using Core.Enums;
using Core.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : BaseApiController
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> PurchaseFeature([FromBody] PurchaseRequest request)
        {
            try
            {
                // Validate authentication
                var authError = ValidateUserAuth(out var userId);
                if (authError != null) return authError;

                // Validate request
                var validationError = ValidateRequired(
                    (request, "request"),
                    (request?.PaymentToken, "paymentToken")
                );
                if (validationError != null) return validationError;

                // Additional validations
                if (request!.Months <= 0 || request.Months > 24)
                    return Error("Subscription duration must be between 1 and 24 months.", 400, "INVALID_DURATION");

                if (!Enum.IsDefined(typeof(FeatureType), request.Feature))
                    return Error("Invalid feature type.", 400, "INVALID_FEATURE");

                var amount = _paymentService.CalculateAmount(request.Feature, request.Months);

                var paymentRequest = new PaymentRequest
                {
                    UserId = userId,
                    Feature = request.Feature,
                    Months = request.Months,
                    PaymentToken = request.PaymentToken,
                    Amount = amount
                };

                var response = await _paymentService.ProcessPaymentAsync(paymentRequest);

                if (response.Success)
                {
                    var successData = new
                    {
                        Feature = request.Feature.ToString(),
                        Months = request.Months,
                        Amount = amount,
                        TransactionId = response.TransactionId,
                        ExpiryDate = DateTime.UtcNow.AddMonths(request.Months)
                    };

                    return Success(successData, $"Successfully purchased {request.Feature} subscription for {request.Months} months.");
                }

                return Error($"Payment failed: {response.Message}", 400, "PAYMENT_FAILED");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to process payment");
            }
        }

        [HttpGet("price/{feature}/{months}")]
        public IActionResult GetPrice([FromRoute] FeatureType feature, [FromRoute] int months)
        {
            try
            {
                // Validate parameters
                if (!Enum.IsDefined(typeof(FeatureType), feature))
                    return Error("Invalid feature type.", 400, "INVALID_FEATURE");

                if (months <= 0 || months > 24)
                    return Error("Duration must be between 1 and 24 months.", 400, "INVALID_DURATION");

                var amount = _paymentService.CalculateAmount(feature, months);
                var priceData = new
                {
                    Feature = feature.ToString(),
                    Months = months,
                    MonthlyPrice = amount / months,
                    TotalAmount = amount,
                    Currency = "USD"
                };

                return Success(priceData, $"Price calculated for {feature} subscription.");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Failed to calculate price");
            }
        }
    }

    public class PurchaseRequest
    {
        public FeatureType Feature { get; set; }
        public int Months { get; set; }
        public string PaymentToken { get; set; } = string.Empty;
    }
}