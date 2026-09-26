using Eger.Application.Exceptions;

namespace Eger.Application.Common;

public static class Ids
{
    public static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length == 24 && id.All(Uri.IsHexDigit);

    public static void Ensure(string? id)
    {
        if (!IsValid(id))
            throw new AppException(400, "Некоректний ідентифікатор");
    }
}
