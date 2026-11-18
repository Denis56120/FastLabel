namespace FastLabel.Backend.Helpers;

public static class ClaimsPrincipalHelpers
{
    public static (int UserId, bool IsAdmin, bool IsReseller) GetUserInfo(this ClaimsPrincipal user)
    {
        var userIdClaim = GetUserIdByClaimPrincipal(user);

        int.TryParse(userIdClaim, out var userId);
        
        bool isAdmin = user.IsInRole("Admin");
        bool isReseller = user.IsInRole("Reseller");

        return (userId, isAdmin, isReseller);
    }

    public static string GetUserIdByClaimPrincipal(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}