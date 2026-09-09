using HealingNaturalFarms.Api.Services;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealingNaturalFarms.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(AppDbContext db, IPushNotificationSender pushSender) : ControllerBase
{
    /// <summary>
    /// Called by the MAUI app once it has a Firebase registration token
    /// (on first launch and whenever the token refreshes). Anonymous is
    /// allowed so guests can still receive order-status pushes for orders
    /// they placed without an account; UserId is attached when logged in.
    /// </summary>
    [HttpPost("devices")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterDevice(RegisterDeviceRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        var existing = await db.DeviceRegistrations.FirstOrDefaultAsync(d => d.PushToken == request.PushToken, ct);
        if (existing is not null)
        {
            existing.UserId = userId ?? existing.UserId;
            existing.DeviceModel = request.DeviceModel;
            existing.AppVersion = request.AppVersion;
            existing.LastSeenAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.DeviceRegistrations.Add(new DeviceRegistration
            {
                UserId = userId,
                Platform = request.Platform,
                PushToken = request.PushToken,
                DeviceModel = request.DeviceModel,
                AppVersion = request.AppVersion
            });
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Admin/support utility to confirm push delivery works
    /// end-to-end for a given token before relying on it for order
    /// notifications - lock this down with a real admin policy before
    /// shipping (left as [Authorize] only, no role check, as a starting
    /// point).</summary>
    [HttpPost("test")]
    [Authorize]
    public async Task<IActionResult> SendTest([FromQuery] string deviceToken, CancellationToken ct)
    {
        await pushSender.SendAsync(deviceToken, "Healing Natural Farms", "Push notifications are working.", null, ct);
        return NoContent();
    }

    private int? GetUserId()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(sub, out var id) ? id : null;
    }
}
