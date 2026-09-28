using Eger.Application.Abstractions;
using Eger.Application.Exceptions;
using Eger.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Eger.Infrastructure.Mongo;

public class UserRepository : IUserRepository
{
    private readonly MongoContext _context;
    public UserRepository(MongoContext context) => _context = context;

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _context.Users.Find(x => x.Email == email).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var list = ids.Where(id => ObjectId.TryParse(id, out _)).Distinct().ToList();
        if (list.Count == 0)
            return [];
        return await _context.Users.Find(Builders<User>.Filter.In(x => x.Id, list)).ToListAsync(ct);
    }

    public Task CreateAsync(User user, CancellationToken ct = default) => InsertAsync(_context.Users, user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default) =>
        ReplaceAsync(_context.Users, user.Id, user, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.Users.DeleteOneAsync(x => x.Id == id, ct);

    public async Task<bool> EmailExistsAsync(string email, string? exceptUserId = null, CancellationToken ct = default)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Email, email);
        if (!string.IsNullOrEmpty(exceptUserId))
            filter &= Builders<User>.Filter.Ne(x => x.Id, exceptUserId);
        return await _context.Users.Find(filter).AnyAsync(ct);
    }

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_users_email" }),
            cancellationToken: ct);

    internal static async Task InsertAsync<T>(IMongoCollection<T> collection, T entity, CancellationToken ct) where T : class
    {
        var id = typeof(T).GetProperty(nameof(User.Id));
        if (id?.GetValue(entity) is not string value || string.IsNullOrEmpty(value))
            id?.SetValue(entity, ObjectId.GenerateNewId().ToString());

        try
        {
            await collection.InsertOneAsync(entity, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new AppException(409, "Запис із такими унікальними даними вже існує");
        }
    }

    internal static async Task ReplaceAsync<T>(IMongoCollection<T> collection, string id, T entity, CancellationToken ct) where T : class
    {
        try
        {
            var filter = Builders<T>.Filter.Eq("_id", ObjectId.Parse(id));
            await collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new AppException(409, "Запис із такими унікальними даними вже існує");
        }
    }
}

public class StudentRepository : IStudentRepository
{
    private readonly MongoContext _context;
    public StudentRepository(MongoContext context) => _context = context;

    public async Task<Student?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.Students.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public Task<Student?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        _context.Students.Find(x => x.UserId == userId).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Students.Find(Builders<Student>.Filter.Empty).ToListAsync(ct);

    public async Task<IReadOnlyList<Student>> GetByGroupAsync(string group, CancellationToken ct = default) =>
        await _context.Students.Find(x => x.Group == group).ToListAsync(ct);

    public async Task<IReadOnlyList<Student>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var list = ids.Where(id => ObjectId.TryParse(id, out _)).Distinct().ToList();
        if (list.Count == 0)
            return [];
        return await _context.Students.Find(Builders<Student>.Filter.In(x => x.Id, list)).ToListAsync(ct);
    }

    public async Task<bool> CardExistsAsync(string card, string? exceptId = null, CancellationToken ct = default)
    {
        var filter = Builders<Student>.Filter.Eq(x => x.StudentCardNumber, card);
        if (!string.IsNullOrEmpty(exceptId))
            filter &= Builders<Student>.Filter.Ne(x => x.Id, exceptId);
        return await _context.Students.Find(filter).AnyAsync(ct);
    }

    public Task CreateAsync(Student student, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.Students, student, ct);

    public Task UpdateAsync(Student student, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.Students, student.Id, student, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.Students.DeleteOneAsync(x => x.Id == id, ct);

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _context.Students.CountDocumentsAsync(Builders<Student>.Filter.Empty, cancellationToken: ct);

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await _context.Students.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Student>(
                Builders<Student>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_students_userId" }),
            new CreateIndexModel<Student>(
                Builders<Student>.IndexKeys.Ascending(x => x.StudentCardNumber),
                new CreateIndexOptions { Unique = true, Name = "ux_students_card" })
        ], ct);
    }
}

public class ProfessorRepository : IProfessorRepository
{
    private readonly MongoContext _context;
    public ProfessorRepository(MongoContext context) => _context = context;

    public async Task<Professor?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.Professors.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public Task<Professor?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        _context.Professors.Find(x => x.UserId == userId).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<Professor>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Professors.Find(Builders<Professor>.Filter.Empty).ToListAsync(ct);

    public async Task<IReadOnlyList<Professor>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var list = ids.Where(id => ObjectId.TryParse(id, out _)).Distinct().ToList();
        if (list.Count == 0)
            return [];
        return await _context.Professors.Find(Builders<Professor>.Filter.In(x => x.Id, list)).ToListAsync(ct);
    }

    public Task CreateAsync(Professor professor, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.Professors, professor, ct);

    public Task UpdateAsync(Professor professor, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.Professors, professor.Id, professor, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.Professors.DeleteOneAsync(x => x.Id == id, ct);

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _context.Professors.CountDocumentsAsync(Builders<Professor>.Filter.Empty, cancellationToken: ct);

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.Professors.Indexes.CreateOneAsync(
            new CreateIndexModel<Professor>(
                Builders<Professor>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_professors_userId" }),
            cancellationToken: ct);
}

public class SubjectRepository : ISubjectRepository
{
    private readonly MongoContext _context;
    public SubjectRepository(MongoContext context) => _context = context;

    public async Task<Subject?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.Subjects.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Subjects.Find(Builders<Subject>.Filter.Empty).ToListAsync(ct);

    public async Task<IReadOnlyList<Subject>> GetByProfessorAsync(string professorId, CancellationToken ct = default) =>
        await _context.Subjects.Find(Builders<Subject>.Filter.AnyEq(x => x.ProfessorIds, professorId)).ToListAsync(ct);

    public Task<bool> AnyWithProfessorAsync(string professorId, CancellationToken ct = default) =>
        _context.Subjects.Find(Builders<Subject>.Filter.AnyEq(x => x.ProfessorIds, professorId)).AnyAsync(ct);

    public Task CreateAsync(Subject subject, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.Subjects, subject, ct);

    public Task UpdateAsync(Subject subject, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.Subjects, subject.Id, subject, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.Subjects.DeleteOneAsync(x => x.Id == id, ct);

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _context.Subjects.CountDocumentsAsync(Builders<Subject>.Filter.Empty, cancellationToken: ct);

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.Subjects.Indexes.CreateOneAsync(
            new CreateIndexModel<Subject>(
                Builders<Subject>.IndexKeys.Ascending(x => x.Title),
                new CreateIndexOptions { Name = "ix_subjects_title" }),
            cancellationToken: ct);
}

public class GradeRepository : IGradeRepository
{
    private readonly MongoContext _context;
    public GradeRepository(MongoContext context) => _context = context;

    public async Task<Grade?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.Grades.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Grade>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Grades.Find(Builders<Grade>.Filter.Empty).ToListAsync(ct);

    public async Task<IReadOnlyList<Grade>> GetByStudentAsync(string studentId, CancellationToken ct = default) =>
        await _context.Grades.Find(x => x.StudentId == studentId).ToListAsync(ct);

    public async Task<IReadOnlyList<Grade>> GetBySubjectAsync(string subjectId, CancellationToken ct = default) =>
        await _context.Grades.Find(x => x.SubjectId == subjectId).ToListAsync(ct);

    public Task<bool> AnyBySubjectAsync(string subjectId, CancellationToken ct = default) =>
        _context.Grades.Find(x => x.SubjectId == subjectId).AnyAsync(ct);

    public Task<bool> AnyByProfessorAsync(string professorId, CancellationToken ct = default) =>
        _context.Grades.Find(x => x.ProfessorId == professorId).AnyAsync(ct);

    public Task CreateAsync(Grade grade, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.Grades, grade, ct);

    public Task UpdateAsync(Grade grade, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.Grades, grade.Id, grade, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.Grades.DeleteOneAsync(x => x.Id == id, ct);

    public Task DeleteByStudentAsync(string studentId, CancellationToken ct = default) =>
        _context.Grades.DeleteManyAsync(x => x.StudentId == studentId, ct);

    public Task DeleteBySessionAsync(string sessionId, CancellationToken ct = default) =>
        _context.Grades.DeleteManyAsync(x => x.SessionId == sessionId, ct);

    public Task DeleteAllAsync(CancellationToken ct = default) =>
        _context.Grades.DeleteManyAsync(Builders<Grade>.Filter.Empty, ct);

    public Task<long> CountAsync(CancellationToken ct = default) =>
        _context.Grades.CountDocumentsAsync(Builders<Grade>.Filter.Empty, cancellationToken: ct);

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.Grades.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Grade>(
                Builders<Grade>.IndexKeys.Ascending(x => x.StudentId),
                new CreateIndexOptions { Name = "ix_grades_studentId" }),
            new CreateIndexModel<Grade>(
                Builders<Grade>.IndexKeys.Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "ix_grades_subjectId" })
        ], ct);
}

public class ClassSessionRepository : IClassSessionRepository
{
    private readonly MongoContext _context;
    public ClassSessionRepository(MongoContext context) => _context = context;

    public async Task<ClassSession?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _))
            return null;
        return await _context.ClassSessions.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ClassSession>> GetBySubjectGroupAsync(string subjectId, string group, CancellationToken ct = default) =>
        await _context.ClassSessions.Find(x => x.SubjectId == subjectId && x.Group == group).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetGroupsBySubjectAsync(string subjectId, CancellationToken ct = default)
    {
        var sessions = await _context.ClassSessions.Find(x => x.SubjectId == subjectId).ToListAsync(ct);
        return sessions
            .Select(session => session.Group)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<ClassSession>> GetByGroupAsync(string group, CancellationToken ct = default) =>
        await _context.ClassSessions.Find(x => x.Group == group).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetSubjectIdsByGroupAsync(string group, CancellationToken ct = default)
    {
        var sessions = await _context.ClassSessions.Find(x => x.Group == group).ToListAsync(ct);
        return sessions
            .Select(session => session.SubjectId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public Task CreateAsync(ClassSession session, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.ClassSessions, session, ct);

    public Task UpdateAsync(ClassSession session, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.ClassSessions, session.Id, session, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.ClassSessions.DeleteOneAsync(x => x.Id == id, ct);

    public Task DeleteAllAsync(CancellationToken ct = default) =>
        _context.ClassSessions.DeleteManyAsync(Builders<ClassSession>.Filter.Empty, ct);

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.ClassSessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ClassSession>(
                Builders<ClassSession>.IndexKeys.Ascending(x => x.SubjectId).Ascending(x => x.Group),
                new CreateIndexOptions { Name = "ix_sessions_subject_group" }),
            cancellationToken: ct);
}

public class JournalSheetRepository : IJournalSheetRepository
{
    private readonly MongoContext _context;
    public JournalSheetRepository(MongoContext context) => _context = context;

    public async Task<JournalSheet?> GetBySubjectGroupAsync(string subjectId, string group, CancellationToken ct = default) =>
        await _context.JournalSheets.Find(x => x.SubjectId == subjectId && x.Group == group).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<JournalSheet>> GetByGroupAsync(string group, CancellationToken ct = default) =>
        await _context.JournalSheets.Find(x => x.Group == group).ToListAsync(ct);

    public Task CreateAsync(JournalSheet sheet, CancellationToken ct = default) =>
        UserRepository.InsertAsync(_context.JournalSheets, sheet, ct);

    public Task UpdateAsync(JournalSheet sheet, CancellationToken ct = default) =>
        UserRepository.ReplaceAsync(_context.JournalSheets, sheet.Id, sheet, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _context.JournalSheets.DeleteOneAsync(x => x.Id == id, ct);

    public Task DeleteAllAsync(CancellationToken ct = default) =>
        _context.JournalSheets.DeleteManyAsync(Builders<JournalSheet>.Filter.Empty, ct);

    public Task EnsureIndexesAsync(CancellationToken ct = default) =>
        _context.JournalSheets.Indexes.CreateOneAsync(
            new CreateIndexModel<JournalSheet>(
                Builders<JournalSheet>.IndexKeys.Ascending(x => x.SubjectId).Ascending(x => x.Group),
                new CreateIndexOptions { Unique = true, Name = "ux_sheets_subject_group" }),
            cancellationToken: ct);
}

public class GradingSettingsRepository : IGradingSettingsRepository
{
    private readonly MongoContext _context;

    public GradingSettingsRepository(MongoContext context) => _context = context;

    public async Task<GradingSettings> GetAsync(CancellationToken ct = default)
    {
        var existing = await _context.GradingSettings
            .Find(item => item.Id == GradingSettings.SingletonId)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var created = new GradingSettings();
        await _context.GradingSettings.InsertOneAsync(created, cancellationToken: ct);
        return created;
    }

    public Task UpdateAsync(GradingSettings settings, CancellationToken ct = default)
    {
        settings.Id = GradingSettings.SingletonId;
        return _context.GradingSettings.ReplaceOneAsync(
            item => item.Id == GradingSettings.SingletonId,
            settings,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}
