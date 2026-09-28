using System.Globalization;
using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class StudentService
{
    private static readonly CompareInfo Uk = CultureInfo.GetCultureInfo("uk-UA").CompareInfo;

    private readonly IStudentRepository _students;
    private readonly IUserRepository _users;
    private readonly IGradeRepository _grades;
    private readonly IPasswordHasher _hasher;

    public StudentService(
        IStudentRepository students,
        IUserRepository users,
        IGradeRepository grades,
        IPasswordHasher hasher)
    {
        _students = students;
        _users = users;
        _grades = grades;
        _hasher = hasher;
    }

    public async Task<IReadOnlyList<StudentResponse>> ListAsync(CancellationToken ct = default)
    {
        var students = await _students.GetAllAsync(ct);
        var mapped = await MapManyAsync(students, ct);
        return mapped
            .OrderBy(s => s.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FullName, Comparer<string>.Create((a, b) => Uk.Compare(a, b)))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GroupsAsync(CancellationToken ct = default)
    {
        var students = await _students.GetAllAsync(ct);
        return students
            .Select(s => s.Group)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<StudentResponse> GetAsync(string id, Actor actor, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var student = await _students.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        await EnsureCanViewAsync(student, actor, ct);
        var user = await _users.GetByIdAsync(student.UserId, ct);
        return Map(student, user);
    }

    public async Task<StudentResponse> MeAsync(string userId, CancellationToken ct = default)
    {
        var student = await _students.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль студента не знайдено");
        var user = await _users.GetByIdAsync(student.UserId, ct);
        return Map(student, user);
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var card = NormalizeCard(request.StudentCardNumber);
        if (await _users.EmailExistsAsync(email, null, ct))
            throw new AppException(409, "Користувач із такою поштою вже існує");
        if (await _students.CardExistsAsync(card, null, ct))
            throw new AppException(409, "Залікова книжка з таким номером вже існує");

        var user = new User
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = Roles.Student,
            Is2FAEnabled = false
        };

        try
        {
            await _users.CreateAsync(user, ct);
            var student = new Student
            {
                UserId = user.Id,
                FullName = request.FullName.Trim(),
                Group = request.Group.Trim(),
                StudentCardNumber = card,
                EnrollmentYear = request.EnrollmentYear
            };
            await _students.CreateAsync(student, ct);
            return Map(student, user);
        }
        catch
        {
            if (!string.IsNullOrEmpty(user.Id))
                await _users.DeleteAsync(user.Id, ct);
            throw;
        }
    }

    public async Task<StudentResponse> UpdateAsync(string id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var student = await _students.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        var user = await _users.GetByIdAsync(student.UserId, ct)
            ?? throw new AppException(404, "Обліковий запис студента не знайдено");

        var email = NormalizeEmail(request.Email);
        var card = NormalizeCard(request.StudentCardNumber);
        if (await _users.EmailExistsAsync(email, user.Id, ct))
            throw new AppException(409, "Користувач із такою поштою вже існує");
        if (await _students.CardExistsAsync(card, student.Id, ct))
            throw new AppException(409, "Залікова книжка з таким номером вже існує");

        user.Email = email;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _hasher.Hash(request.Password);
        student.FullName = request.FullName.Trim();
        student.Group = request.Group.Trim();
        student.StudentCardNumber = card;
        student.EnrollmentYear = request.EnrollmentYear;

        await _users.UpdateAsync(user, ct);
        await _students.UpdateAsync(student, ct);
        return Map(student, user);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var student = await _students.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Студента не знайдено");
        await _grades.DeleteByStudentAsync(student.Id, ct);
        await _students.DeleteAsync(student.Id, ct);
        await _users.DeleteAsync(student.UserId, ct);
    }

    private async Task EnsureCanViewAsync(Student student, Actor actor, CancellationToken ct)
    {
        if (actor.Role is Roles.Admin or Roles.Professor)
            return;
        if (actor.Role == Roles.Student && student.UserId == actor.UserId)
            return;
        var own = await _students.GetByUserIdAsync(actor.UserId, ct);
        if (own?.Id == student.Id)
            return;
        throw new AppException(403, "Недостатньо прав");
    }

    private async Task<List<StudentResponse>> MapManyAsync(IReadOnlyList<Student> students, CancellationToken ct)
    {
        var users = await _users.GetByIdsAsync(students.Select(s => s.UserId), ct);
        var byId = users.ToDictionary(u => u.Id);
        return students.Select(s =>
        {
            byId.TryGetValue(s.UserId, out var user);
            return Map(s, user);
        }).ToList();
    }

    private static StudentResponse Map(Student student, User? user) => new()
    {
        Id = student.Id,
        UserId = student.UserId,
        Email = user?.Email ?? "",
        FullName = student.FullName,
        Group = student.Group,
        StudentCardNumber = student.StudentCardNumber,
        EnrollmentYear = student.EnrollmentYear,
        Is2FAEnabled = user?.Is2FAEnabled ?? false
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeCard(string card) => card.Trim().ToUpperInvariant();
}
