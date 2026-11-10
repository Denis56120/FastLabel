namespace FastLabel.Shared.DTOs;

public record AuthResponseDto(
    string Token,
    Guid UserId,
    string Email,
    string Language,
    DateTime ExpiresAt
);