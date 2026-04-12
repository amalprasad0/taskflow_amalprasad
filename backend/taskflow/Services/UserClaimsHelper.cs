using System.Security.Claims;

namespace taskFlow.Services
{
    public static class UserClaimsHelper
    {
        public static Guid GetUserId(this HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? context.User.FindFirst("UserId")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
                return userId;

            return Guid.Empty;
        }

        public static string? GetUserEmail(this HttpContext context)
        {
            return context.User.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static bool IsAuthenticated(this HttpContext context)
        {
            return context.User.Identity?.IsAuthenticated == true;
        }

        // Alternative method using context.Items (set by middleware)
        public static Guid GetUserIdFromContext(this HttpContext context)
        {
            var userId = context.Items["UserId"] as string;
            if (Guid.TryParse(userId, out var guid))
                return guid;

            return Guid.Empty;
        }

        public static string? GetUserEmailFromContext(this HttpContext context)
        {
            return context.Items["Email"] as string;
        }
    }
}
