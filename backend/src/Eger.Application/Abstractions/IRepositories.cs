using Eger.Domain.Entities;

namespace Eger.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, string? exceptUserId = null, CancellationToken ct = default);
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Student?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Student>> GetByGroupAsync(string group, CancellationToken ct = default);
    Task<IReadOnlyList<Student>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<bool> CardExistsAsync(string card, string? exceptId = null, CancellationToken ct = default);
    Task CreateAsync(Student student, CancellationToken ct = default);
    Task UpdateAsync(Student student, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

public interface IProfessorRepository
{
    Task<Professor?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Professor?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Professor>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Professor>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task CreateAsync(Professor professor, CancellationToken ct = default);
    Task UpdateAsync(Professor professor, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

public interface ISubjectRepository
{
    Task<Subject?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Subject>> GetByProfessorAsync(string professorId, CancellationToken ct = default);
    Task<bool> AnyWithProfessorAsync(string professorId, CancellationToken ct = default);
    Task CreateAsync(Subject subject, CancellationToken ct = default);
    Task UpdateAsync(Subject subject, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

public interface IGradeRepository
{
    Task<Grade?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Grade>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Grade>> GetByStudentAsync(string studentId, CancellationToken ct = default);
    Task<IReadOnlyList<Grade>> GetBySubjectAsync(string subjectId, CancellationToken ct = default);
    Task<bool> AnyBySubjectAsync(string subjectId, CancellationToken ct = default);
    Task<bool> AnyByProfessorAsync(string professorId, CancellationToken ct = default);
    Task CreateAsync(Grade grade, CancellationToken ct = default);
    Task UpdateAsync(Grade grade, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task DeleteByStudentAsync(string studentId, CancellationToken ct = default);
    Task DeleteAllAsync(CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task EnsureIndexesAsync(CancellationToken ct = default);
}

public interface IGradingSettingsRepository
{
    Task<Eger.Domain.Entities.GradingSettings> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(Eger.Domain.Entities.GradingSettings settings, CancellationToken ct = default);
}
