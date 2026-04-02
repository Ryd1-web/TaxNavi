using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPaymentService paymentService,
            IPaymentRepository paymentRepository,
            ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _paymentRepository = paymentRepository;
            _logger = logger;
        }

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentDto request)
        {
            try
            {
                _logger.LogInformation("Initiating payment for user: {UserId}, amount: {Amount}", request.UserId, request.Amount);

                var paymentRequest = new Domain.Interfaces.PaymentRequest
                {
                    Amount = request.Amount,
                    Email = "user@example.com", // Should come from user context
                    Reference = $"TAX-{Guid.NewGuid():N}"
                };

                var result = await _paymentService.InitiatePaymentAsync(paymentRequest);

                if (string.IsNullOrEmpty(result.Reference))
                {
                    return BadRequest(new { success = false, message = "Failed to initiate payment" });
                }

                // Save payment record
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    Amount = request.Amount,
                    Status = "Pending",
                    Reference = result.Reference,
                    CreatedAt = DateTime.UtcNow
                };

                await _paymentRepository.AddAsync(payment);
                await _paymentRepository.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Payment initiated successfully",
                    data = new
                    {
                        reference = result.Reference,
                        authorizationUrl = result.AuthorizationUrl,
                        accessCode = result.AccessCode,
                        amount = request.Amount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating payment");
                return StatusCode(500, new { success = false, message = "An error occurred while initiating payment" });
            }
        }

        [HttpPost("verify")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto request)
        {
            try
            {
                _logger.LogInformation("Verifying payment with reference: {Reference}", request.Reference);

                var result = await _paymentService.VerifyPaymentAsync(request.Reference);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = "Payment verification failed" });
                }

                // Update payment record
                var payment = await _paymentRepository.GetByReferenceAsync(request.Reference);

                if (payment != null)
                {
                    payment.Status = result.Status ?? "Completed";
                    await _paymentRepository.UpdateAsync(payment);
                    await _paymentRepository.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = "Payment verified successfully",
                    data = new
                    {
                        reference = result.Reference,
                        status = result.Status,
                        amount = payment?.Amount ?? 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment");
                return StatusCode(500, new { success = false, message = "An error occurred while verifying payment" });
            }
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetPaymentHistory(Guid userId)
        {
            try
            {
                var payments = await _paymentRepository.GetByUserIdAsync(userId);

                return Ok(new
                {
                    success = true,
                    data = payments.Select(p => new
                    {
                        id = p.Id,
                        amount = p.Amount,
                        status = p.Status,
                        reference = p.Reference,
                        createdAt = p.CreatedAt
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment history");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving payment history" });
            }
        }

        [HttpGet("{paymentId}")]
        public async Task<IActionResult> GetPayment(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);

                if (payment == null)
                {
                    return NotFound(new { success = false, message = "Payment not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = payment.Id,
                        amount = payment.Amount,
                        status = payment.Status,
                        reference = payment.Reference,
                        createdAt = payment.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving payment" });
            }
        }
    }
}
