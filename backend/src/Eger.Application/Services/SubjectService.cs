using System.Globalization;
using Eger.Application.Abstractions;
using Eger.Application.Common;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class SubjectService
{
    private static readonly CompareInfo Uk = CultureInfo.GetCultureInfo("uk-UA").CompareInfo;

    private readonly ISubjectRepository _subjects;
    private readonly IProfessorRepository _professors;
    private readonly IGradeRepository _grades;

    public SubjectService(ISubjectRepository subjects, IProfessorRepository professors, IGradeRepository grades)
    {
        _subjects = subjects;
        _professors = professors;
        _grades = grades;
    }

    public async Task<IReadOnlyList<SubjectResponse>> ListAsync(CancellationToken ct = default)
    {
        var subjects = await _subjects.GetAllAsync(ct);
        return await MapManyAsync(subjects, ct);
    }

    public async Task<IReadOnlyList<SubjectResponse>> MineAsync(string userId, CancellationToken ct = default)
    {
        var professor = await _professors.GetByUserIdAsync(userId, ct)
            ?? throw new AppException(404, "Профіль викладача не знайдено");
        var subjects = await _subjects.GetByProfessorAsync(professor.Id, ct);
        return await MapManyAsync(subjects, ct);
    }

    public async Task<SubjectResponse> GetAsync(string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var subject = await _subjects.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        var mapped = await MapManyAsync([subject], ct);
        return mapped[0];
    }

    public async Task<SubjectResponse> CreateAsync(SubjectRequest request, CancellationToken ct = default)
    {
        var professorIds = await NormalizeProfessorsAsync(request.ProfessorIds, ct);
        var subject = new Subject
        {
            Title = request.Title.Trim(),
            ProfessorIds = professorIds,
            Credits = request.Credits
        };
        await _subjects.CreateAsync(subject, ct);
        var mapped = await MapManyAsync([subject], ct);
        return mapped[0];
    }

    public async Task<SubjectResponse> UpdateAsync(string id, SubjectRequest request, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var subject = await _subjects.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        subject.Title = request.Title.Trim();
        subject.ProfessorIds = await NormalizeProfessorsAsync(request.ProfessorIds, ct);
        subject.Credits = request.Credits;
        await _subjects.UpdateAsync(subject, ct);
        var mapped = await MapManyAsync([subject], ct);
        return mapped[0];
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        Ids.Ensure(id);
        var subject = await _subjects.GetByIdAsync(id, ct)
            ?? throw new AppException(404, "Дисципліну не знайдено");
        if (await _grades.AnyBySubjectAsync(subject.Id, ct))
            throw new AppException(409, "Неможливо видалити дисципліну, до якої вже виставлено оцінки");
        await _subjects.DeleteAsync(subject.Id, ct);
    }

    private async Task<List<string>> NormalizeProfessorsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var distinct = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var id in distinct)
            Ids.Ensure(id);

        if (distinct.Count == 0)
            throw new AppException(400, "Призначте хоча б одного викладача");

        var professors = await _professors.GetByIdsAsync(distinct, ct);
        if (professors.Count != distinct.Count)
            throw new AppException(400, "Один або кілька викладачів не знайдені");

        return distinct;
    }

    private async Task<List<SubjectResponse>> MapManyAsync(IReadOnlyList<Subject> subjects, CancellationToken ct)
    {
        var ids = subjects.SelectMany(s => s.ProfessorIds).Distinct();
        var professors = await _professors.GetByIdsAsync(ids, ct);
        var names = professors.ToDictionary(p => p.Id, p => p.FullName);

        return subjects
            .OrderBy(s => s.Title, Comparer<string>.Create((a, b) => Uk.Compare(a, b)))
            .Select(s => new SubjectResponse
            {
                Id = s.Id,
                Title = s.Title,
                Credits = s.Credits,
                ProfessorIds = s.ProfessorIds,
                ProfessorNames = s.ProfessorIds.Select(id => names.TryGetValue(id, out var name) ? name : "—").ToList()
            })
            .ToList();
    }
}
