using HealingNaturalFarms.Api.Services;
using HealingNaturalFarms.Domain.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealingNaturalFarms.Api.Controllers;

[ApiController]
[Route("api/checkout")]
[AllowAnonymous] // guest checkout allowed; see the note on CartController
public class CheckoutController(ICheckoutService checkoutService) : ControllerBase
{
    /// <summary>
    /// Step 1: server re-validates the cart and creates the provider-side
    /// payment object. Returns whatever the client SDK needs next -
    /// Stripe's client secret for its Payment Element, or a PayPal/
    /// Razorpay order id to open their checkout widget.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateCheckoutResponse>> CreateCheckout(CreateCheckoutRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var response = await checkoutService.CreateCheckoutAsync(userId, request, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Step 2: after the client completes payment with the provider's SDK
    /// (Stripe.js confirmCardPayment, PayPal Buttons onApprove, Razorpay
    /// Checkout handler), it calls this to have the server verify the
    /// result server-side and mark the order paid. Never trust a client's
    /// bare claim of success - see each gateway's VerifyAndCaptureAsync.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<OrderDetailDto>> ConfirmPayment(ConfirmPaymentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await checkoutService.ConfirmPaymentAsync(request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(sub, out var id) ? id : null;
    }
}
