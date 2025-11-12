using Core.Enums;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> PurchaseFeature([FromBody] PurchaseRequest request)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

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
                return Ok(new
                {
                    message = $"Successfully purchased {request.Feature} for {request.Months} months",
                    transactionId = response.TransactionId
                });
            }

            return BadRequest(new { message = response.Message });
        }

        [HttpGet("price/{feature}/{months}")]
        public IActionResult GetPrice([FromRoute] FeatureType feature, [FromRoute] int months)
        {
            var amount = _paymentService.CalculateAmount(feature, months);
            return Ok(new { feature, months, amount });
        }
    }

    public class PurchaseRequest
    {
        public FeatureType Feature { get; set; }
        public int Months { get; set; }
        public string PaymentToken { get; set; } = string.Empty;
    }
}