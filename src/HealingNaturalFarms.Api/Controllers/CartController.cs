using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealingNaturalFarms.Api.Controllers;

/// <summary>
/// Carts work for both guests and logged-in users. A guest identifies
/// itself with an X-Guest-Id header (a GUID the client generates once and
/// persists in local storage / MAUI Preferences); an authenticated
/// request's user id comes from the JWT instead and takes precedence.
/// [AllowAnonymous] throughout - checkout itself (CheckoutController)
/// is where an account may eventually be required if the business wants
/// to disallow guest checkout, but that's a policy choice, not a
/// technical one, so it's left open here.
/// </summary>
[ApiController]
[Route("api/cart")]
[AllowAnonymous]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart([FromQuery] RegionCode region, [FromHeader(Name = "X-Guest-Id")] string? guestId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null && string.IsNullOrWhiteSpace(guestId))
        {
            return BadRequest(new { message = "Either an authenticated session or an X-Guest-Id header is required." });
        }

        var cart = await cartService.GetOrCreateCartAsync(userId, guestId, region, ct);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(
        [FromQuery] RegionCode region, [FromHeader(Name = "X-Guest-Id")] string? guestId,
        AddCartItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var cart = await cartService.GetOrCreateCartAsync(userId, guestId, region, ct);
        var updated = await cartService.AddItemAsync(cart.CartId, request.ProductId, request.Quantity, ct);
        return Ok(updated);
    }

    [HttpPut("{cartId:int}/items/{productId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int cartId, int productId, UpdateCartItemRequest request, CancellationToken ct)
    {
        var updated = await cartService.UpdateItemAsync(cartId, productId, request.Quantity, ct);
        return Ok(updated);
    }

    [HttpDelete("{cartId:int}/items/{productId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int cartId, int productId, CancellationToken ct)
    {
        var updated = await cartService.RemoveItemAsync(cartId, productId, ct);
        return Ok(updated);
    }

    private int? GetUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(sub, out var id) ? id : null;
    }
}
