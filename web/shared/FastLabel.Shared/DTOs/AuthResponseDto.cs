namespace FastLabel.Shared.DTOs;

public record UserInfoResponseDto(
    int UserId,
    string Email,
    bool IsAdmin
);