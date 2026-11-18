namespace FastLabel.Backend.Helpers;

public static class ClaimsPrincipalHelpers
{
    public static (int UserId, bool IsAdmin, bool IsReseller) GetUserInfo(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("nameid")?.Value
                       ?? user.FindFirst("unique_name")?.Value;

        int.TryParse(userIdClaim, out var userId);
        
        bool isAdmin = user.IsInRole("Admin");
        bool isReseller = user.IsInRole("Reseller");

        return (userId, isAdmin, isReseller);
    }
}