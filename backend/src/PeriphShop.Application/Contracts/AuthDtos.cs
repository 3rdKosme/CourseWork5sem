namespace PeriphShop.Application.Contracts;

public record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record UserDto(int Id, string Email, string FullName, string? Phone, string? DefaultAddress, string Role);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

public record UpdateProfileRequest(string FullName, string? Phone, string? DefaultAddress);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
