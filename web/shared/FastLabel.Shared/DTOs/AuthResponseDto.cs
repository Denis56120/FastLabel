namespace FastLabel.Shared.DTOs;

public record AuthResponseDto(
    string Token,
    int UserId,
    string Email,
    DateTime ExpiresAt
);