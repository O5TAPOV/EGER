using System.Globalization;
using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class ProfessorService
{
    private static readonly CompareInfo Uk = CultureInfo.GetCultureInfo("uk-UA").CompareInfo;

    private readonly IProfessorRepository _professors;
    private readonly IUserRepository _users;
    private readonly ISubjectRepository _subjects;
    private readonly IGradeRepository _grades;
    private readonly IPasswordHasher _hasher;

    public ProfessorService(
        IProfessorRepository professors,
        IUserRepository users,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IPasswordHasher hasher)
    {
        _professors = professors;
        _users = users;
        _subjects = subjects;
        _grades = grades;
        _hasher = hasher;
    }

    public async Task<IReadOnlyList<ProfessorResponse>> ListAsync(CancellationToken ct = default)
    {
        var professors = await _professors.GetAllAsync(ct);
        var users = await _users.GetByIdsAsync(professors.Select(p => p.UserId), ct);
        var byId = users.ToDictionary(u => u.Id);
        return professors
            .Select(p =>
            {
                byId.TryGetValue(p.UserId, out var user);
                return Map(p, user);
            })
            .OrderBy(p => p.FullName, Comparer<string>.Create((a, b) => Uk.Compare(a, b)))
            .ToList();
    }

    public async Task<ProfessorResponse> GetAsync(string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var professor = await _professors.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Викладача не знайдено");
        var user = await _users.GetByIdAsync(professor.UserId, ct);
        return Map(professor, user);
    }

    public async Task<ProfessorResponse> MeAsync(string userId, CancellationToken ct = default)
    {
        var professor = await _professors.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль викладача не знайдено");
        var user = await _users.GetByIdAsync(professor.UserId, ct);
        return Map(professor, user);
    }

    public async Task<ProfessorResponse> CreateAsync(CreateProfessorRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, null, ct))
            throw new AppException(409, "Користувач із такою поштою вже існує");

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = Roles.Professor,
            Is2FAEnabled = false
        };

        try
        {
            await _users.CreateAsync(user, ct);
            var professor = new Professor
            {
                UserId = user.Id,
                FullName = request.FullName.Trim(),
                Department = request.Department.Trim(),
                AcademicDegree = request.AcademicDegree.Trim()
            };
            await _professors.CreateAsync(professor, ct);
            return Map(professor, user);
        }
        catch
        {
            if (!string.IsNullOrEmpty(user.Id))
                await _users.DeleteAsync(user.Id, ct);
            throw;
        }
    }

    public async Task<ProfessorResponse> UpdateAsync(string id, UpdateProfessorRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var professor = await _professors.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Викладача не знайдено");
        var user = await _users.GetByIdAsync(professor.UserId, ct)
            ?? throw new AppException(404, "Обліковий запис викладача не знайдено");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, user.Id, ct))
            throw new AppException(409, "Користувач із такою поштою вже існує");

        user.Email = email;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _hasher.Hash(request.Password);
        professor.FullName = request.FullName.Trim();
        professor.Department = request.Department.Trim();
        professor.AcademicDegree = request.AcademicDegree.Trim();

        await _users.UpdateAsync(user, ct);
        await _professors.UpdateAsync(professor, ct);
        return Map(professor, user);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var professor = await _professors.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Викладача не знайдено");
        if (await _subjects.AnyWithProfessorAsync(professor.Id, ct) || await _grades.AnyByProfessorAsync(professor.Id, ct))
            throw new AppException(409, "Неможливо видалити викладача, який призначений на дисципліни або має виставлені оцінки");

        await _professors.DeleteAsync(professor.Id, ct);
        await _users.DeleteAsync(professor.UserId, ct);
    }

    private static ProfessorResponse Map(Professor professor, User? user) => new()
    {
        Id = professor.Id,
        UserId = professor.UserId,
        Email = user?.Email ?? "",
        FullName = professor.FullName,
        Department = professor.Department,
        AcademicDegree = professor.AcademicDegree,
        Is2FAEnabled = user?.Is2FAEnabled ?? false
    };
}
