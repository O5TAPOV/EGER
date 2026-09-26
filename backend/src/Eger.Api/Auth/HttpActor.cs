using System.Security.Claims;
using Eger.Application.Common;
using Eger.Application.Exceptions;

namespace Eger.Api.Auth;

public static class HttpActor
{
    public static Actor Current(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(role))
            throw new AppException(401, "Потрібна авторизація");
        return new Actor(id, role);
    }
}
