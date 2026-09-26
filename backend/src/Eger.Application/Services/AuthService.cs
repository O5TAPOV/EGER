using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ISessionStore _sessions;
    private readonly ITwoFactorService _twoFactor;
    private readonly IEmailSender _email;

    public AuthService(
        IUserRepository users,
        IStudentRepository students,
        IProfessorRepository professors,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ISessionStore sessions,
        ITwoFactorService twoFactor,
        IEmailSender email)
    {
        _users = users;
        _students = students;
        _professors = professors;
        _hasher = hasher;
        _jwt = jwt;
        _sessions = sessions;
        _twoFactor = twoFactor;
        _email = email;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new AppException(401, "Невірна пошта або пароль");

        if (user.Is2FAEnabled)
        {
            return new LoginResponse
            {
                Requires2FA = true,
                UserId = user.Id,
                Message = "Оберіть, куди надіслати код. Він дійсний 5 хвилин."
            };
        }

        return await IssueSessionAsync(user, ct);
    }

    public async Task<LoginResponse> SendCodeAsync(Send2FaRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(request.UserId);
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new AppException(401, "Потрібна повторна авторизація");
        if (!user.Is2FAEnabled)
            throw new AppException(400, "Двофакторну перевірку вимкнено");

        var channel = request.Channel.Trim().ToLowerInvariant();
        if (channel == "telegram")
        {
            if (string.IsNullOrWhiteSpace(user.TelegramChatId))
                throw new AppException(400, "Telegram не прив'язано. Вкажіть ідентифікатор чату в кабінеті або надішліть код на пошту.");
            await _twoFactor.IssueAsync(user.Id, TwoFactorChannel.Telegram, user.TelegramChatId, ct);
            return new LoginResponse
            {
                Requires2FA = true,
                UserId = user.Id,
                Message = "Код надіслано в Telegram. Він дійсний 5 хвилин."
            };
        }

        if (channel == "email")
        {
            if (!_email.IsConfigured)
                throw new AppException(503, "Надсилання коду на пошту не налаштовано.");
            await _twoFactor.IssueAsync(user.Id, TwoFactorChannel.Email, user.Email, ct);
            return new LoginResponse
            {
                Requires2FA = true,
                UserId = user.Id,
                Message = "Код надіслано на пошту. Він дійсний 5 хвилин."
            };
        }

        throw new AppException(400, "Оберіть канал: Telegram або пошту.");
    }

    public async Task<LoginResponse> VerifyAsync(Verify2FaRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(request.UserId);
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new AppException(401, "Невірний або прострочений код");

        if (!user.Is2FAEnabled)
            throw new AppException(400, "Двофакторну перевірку вимкнено");

        var ok = await _twoFactor.VerifyAsync(user.Id, request.Code.Trim(), ct);
        if (!ok)
            throw new AppException(401, "Невірний або прострочений код");

        var response = await IssueSessionAsync(user, ct);
        response.Message = "Вхід підтверджено";
        return response;
    }

    public async Task LogoutAsync(string userId, CancellationToken ct = default)
    {
        await _sessions.RevokeAsync(userId, ct);
    }

    public async Task<MeResponse> MeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new AppException(401, "Потрібна авторизація");
        return await MapProfileAsync(user, ct);
    }

    public async Task<MeResponse> UpdateTwoFactorAsync(string userId, Update2FaRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new AppException(401, "Потрібна авторизація");

        var chatId = string.IsNullOrWhiteSpace(request.TelegramChatId)
            ? null
            : request.TelegramChatId.Trim();

        if (request.Is2FAEnabled && user.Role == Roles.Admin)
            throw new AppException(400, "Для адміністратора двофакторна перевірка недоступна");

        if (chatId is not null && (!long.TryParse(chatId, out var parsed) || parsed <= 0))
            throw new AppException(400, "Telegram Chat ID має бути додатним числом");

        user.Is2FAEnabled = request.Is2FAEnabled;
        user.TelegramChatId = chatId;
        await _users.UpdateAsync(user, ct);
        return await MapProfileAsync(user, ct);
    }

    private async Task<LoginResponse> IssueSessionAsync(User user, CancellationToken ct)
    {
        var profile = await MapProfileAsync(user, ct);
        var token = await _jwt.CreateTokenAsync(user, profile.FullName, ct);
        return new LoginResponse
        {
            Requires2FA = false,
            Token = token,
            User = profile,
            Message = "Вхід виконано"
        };
    }

    private async Task<MeResponse> MapProfileAsync(User user, CancellationToken ct)
    {
        var profile = new MeResponse
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            FullName = user.Email,
            Is2FAEnabled = user.Is2FAEnabled,
            TelegramChatId = user.TelegramChatId
        };

        if (user.Role == Roles.Student)
        {
            var student = await _students.GetByUserIdAsync(user.Id, ct);
            if (student is not null)
            {
                profile.ProfileId = student.Id;
                profile.FullName = student.FullName;
                profile.Group = student.Group;
                profile.StudentCardNumber = student.StudentCardNumber;
                profile.EnrollmentYear = student.EnrollmentYear;
            }
        }
        else if (user.Role == Roles.Professor)
        {
            var professor = await _professors.GetByUserIdAsync(user.Id, ct);
            if (professor is not null)
            {
                profile.ProfileId = professor.Id;
                profile.FullName = professor.FullName;
                profile.Department = professor.Department;
                profile.AcademicDegree = professor.AcademicDegree;
            }
        }
        else if (user.Role == Roles.Admin)
        {
            profile.FullName = "Адміністратор";
        }

        return profile;
    }
}
