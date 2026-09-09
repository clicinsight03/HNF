using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Dtos;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName, RegionCode PreferredRegion);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string Email, string FirstName, RegionCode PreferredRegion);

/// <summary>Registers (or refreshes) a device's push token from the MAUI app.</summary>
public record RegisterDeviceRequest(DevicePlatform Platform, string PushToken, string? DeviceModel, string? AppVersion);
