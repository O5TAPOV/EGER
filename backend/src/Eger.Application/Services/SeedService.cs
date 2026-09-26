using Eger.Application.Abstractions;
using Eger.Application.Dtos;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class SeedService
{
    public const string DemoStudentName = "Остапов Антон";
    public const string StudentPassword = "Student123!";
    public const string ProfessorPassword = "Professor123!";

    private readonly IUserRepository _users;
    private readonly IStudentRepository _students;
    private readonly IProfessorRepository _professors;
    private readonly ISubjectRepository _subjects;
    private readonly IGradeRepository _grades;
    private readonly IClassSessionRepository _sessions;
    private readonly IPasswordHasher _hasher;

    public SeedService(
        IUserRepository users,
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IClassSessionRepository sessions,
        IPasswordHasher hasher)
    {
        _users = users;
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
        _sessions = sessions;
        _hasher = hasher;
    }

    public async Task<SeedResult> GenerateAsync(CancellationToken ct = default)
    {
        var existing = await _students.GetAllAsync(ct);
        if (existing.Any(s => s.FullName == DemoStudentName))
        {
            var grades = await _grades.GetAllAsync(ct);
            var legacy = grades.Count > 0 && !grades.Any(grade => grade.GradeType == GradeTypes.Attendance);
            if (!legacy)
            {
                return new SeedResult
                {
                    AlreadySeeded = true,
                    Message = "Демонстраційні дані вже було згенеровано раніше",
                    Students = existing.Count,
                    Professors = (await _professors.GetAllAsync(ct)).Count,
                    Subjects = (await _subjects.GetAllAsync(ct)).Count,
                    Grades = grades.Count
                };
            }

            await _grades.DeleteAllAsync(ct);
            await _sessions.DeleteAllAsync(ct);
            var written = await WriteJournalAsync(ct);
            return new SeedResult
            {
                AlreadySeeded = false,
                Message = "Журнал перебудовано: поточні бали до 80 і підсумок до 20",
                Students = existing.Count,
                Professors = (await _professors.GetAllAsync(ct)).Count,
                Subjects = (await _subjects.GetAllAsync(ct)).Count,
                Grades = written
            };
        }

        var kovalenko = await AddProfessorAsync("professor@eger.ua", "Коваленко Олена Ігорівна", "Кафедра комп'ютерних наук", "Доцент", ct);
        var melnyk = await AddProfessorAsync("andrii.melnyk@eger.ua", "Мельник Андрій Петрович", "Кафедра програмної інженерії", "Професор", ct);
        var shevchenko = await AddProfessorAsync("maria.shevchenko@eger.ua", "Шевченко Марія Василівна", "Кафедра інформаційних систем", "Кандидат технічних наук", ct);
        var bondarenko = await AddProfessorAsync("ihor.bondarenko@eger.ua", "Бондаренко Ігор Олександрович", "Кафедра комп'ютерних наук", "Доцент", ct);

        await AddSubjectAsync("Теорія баз даних", 5, [kovalenko.Id], ct);
        await AddSubjectAsync("Алгоритми та структури даних", 6, [kovalenko.Id, bondarenko.Id], ct);
        await AddSubjectAsync("Веб-технології", 4, [bondarenko.Id], ct);
        await AddSubjectAsync("Операційні системи", 5, [melnyk.Id], ct);
        await AddSubjectAsync("Дискретна математика", 4, [shevchenko.Id], ct);
        await AddSubjectAsync("Комп'ютерні мережі", 4, [melnyk.Id], ct);
        await AddSubjectAsync("Проєктування інформаційних систем", 5, [shevchenko.Id], ct);

        await AddStudentAsync("anton.ostapov@eger.ua", DemoStudentName, "КН-21", "KN-21015", 2021, ct);
        await AddStudentAsync("daryna.kozak@eger.ua", "Козак Дарина Олегівна", "КН-21", "KN-21008", 2021, ct);
        await AddStudentAsync("maksym.lysenko@eger.ua", "Лисенко Максим Сергійович", "КН-21", "KN-21022", 2021, ct);
        await AddStudentAsync("oksana.kravchuk@eger.ua", "Кравчук Оксана Миколаївна", "КН-21", "KN-21031", 2021, ct);
        await AddStudentAsync("sofia.tkachenko@eger.ua", "Ткаченко Софія Андріївна", "КН-22", "KN-22004", 2022, ct);
        await AddStudentAsync("pavlo.hrytsenko@eger.ua", "Гриценко Павло Іванович", "КН-22", "KN-22017", 2022, ct);
        await AddStudentAsync("yulia.romaniuk@eger.ua", "Романюк Юлія Петрівна", "ПІ-21", "PI-21011", 2021, ct);
        await AddStudentAsync("denys.savchuk@eger.ua", "Савчук Денис Володимирович", "ПІ-21", "PI-21003", 2021, ct);
        await AddStudentAsync("kateryna.moroz@eger.ua", "Мороз Катерина Ігорівна", "ІС-21", "IS-21019", 2021, ct);
        await AddStudentAsync("nazar.polishchuk@eger.ua", "Поліщук Назар Богданович", "ІС-21", "IS-21006", 2021, ct);

        var gradeCount = await WriteJournalAsync(ct);

        return new SeedResult
        {
            AlreadySeeded = false,
            Message = "Демонстраційні дані створено",
            Students = 10,
            Professors = 4,
            Subjects = 7,
            Grades = gradeCount
        };
    }

    private async Task<int> WriteJournalAsync(CancellationToken ct)
    {
        var students = (await _students.GetAllAsync(ct)).ToDictionary(student => student.UserId);
        var users = await _users.GetByIdsAsync(students.Keys, ct);
        var byEmail = new Dictionary<string, Student>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            if (students.TryGetValue(user.Id, out var student))
                byEmail[user.Email] = student;
        }

        var subjects = (await _subjects.GetAllAsync(ct)).ToDictionary(subject => subject.Title, StringComparer.Ordinal);
        var professors = (await _professors.GetAllAsync(ct)).ToDictionary(professor => professor.UserId);
        var professorUsers = await _users.GetByIdsAsync(professors.Keys, ct);
        var professorByEmail = new Dictionary<string, Professor>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in professorUsers)
        {
            if (professors.TryGetValue(user.Id, out var professor))
                professorByEmail[user.Email] = professor;
        }

        var sessionIds = new Dictionary<(string SubjectId, string Group, DateTime Date, string Type), string>();
        var count = 0;
        foreach (var row in DemoRows())
        {
            if (!byEmail.TryGetValue(row.StudentEmail, out var student))
                continue;
            if (!subjects.TryGetValue(row.SubjectTitle, out var subject))
                continue;
            if (!professorByEmail.TryGetValue(row.ProfessorEmail, out var professor))
                continue;

            var key = (subject.Id, student.Group, row.Date, row.Type);
            if (!sessionIds.TryGetValue(key, out var sessionId))
            {
                var session = new ClassSession
                {
                    SubjectId = subject.Id,
                    Group = student.Group,
                    Date = row.Date,
                    GradeType = row.Type,
                    MaxPoints = ColumnMax(row.SubjectTitle, row.Type)
                };
                await _sessions.CreateAsync(session, ct);
                sessionId = session.Id;
                sessionIds[key] = sessionId;
            }

            count += await AddGradeAsync(student, subject, professor, row.Points, row.Type, row.Date, sessionId, ct);
        }

        return count;
    }

    private static int ColumnMax(string subject, string type) => (subject, type) switch
    {
        ("Теорія баз даних", GradeTypes.Attendance) => 12,
        ("Теорія баз даних", GradeTypes.Homework) => 20,
        ("Теорія баз даних", GradeTypes.Practical) => 25,
        ("Теорія баз даних", GradeTypes.Module) => 25,
        ("Теорія баз даних", GradeTypes.Exam) => 20,
        ("Алгоритми та структури даних", GradeTypes.Attendance) => 12,
        ("Алгоритми та структури даних", GradeTypes.Homework) => 20,
        ("Алгоритми та структури даних", GradeTypes.Practical) => 20,
        ("Алгоритми та структури даних", GradeTypes.Module) => 20,
        ("Алгоритми та структури даних", GradeTypes.Exam) => 20,
        ("Веб-технології", GradeTypes.Attendance) => 10,
        ("Веб-технології", GradeTypes.Homework) => 15,
        ("Веб-технології", GradeTypes.Practical) => 15,
        ("Веб-технології", GradeTypes.Credit) => 20,
        ("Операційні системи", GradeTypes.Attendance) => 15,
        ("Операційні системи", GradeTypes.Homework) => 20,
        ("Операційні системи", GradeTypes.Practical) => 20,
        ("Операційні системи", GradeTypes.Module) => 25,
        ("Операційні системи", GradeTypes.Exam) => 20,
        ("Комп'ютерні мережі", GradeTypes.Attendance) => 10,
        ("Комп'ютерні мережі", GradeTypes.Homework) => 15,
        ("Комп'ютерні мережі", GradeTypes.Practical) => 15,
        ("Комп'ютерні мережі", GradeTypes.Exam) => 20,
        ("Дискретна математика", GradeTypes.Attendance) => 12,
        ("Дискретна математика", GradeTypes.Homework) => 20,
        ("Дискретна математика", GradeTypes.Practical) => 20,
        ("Дискретна математика", GradeTypes.Module) => 20,
        ("Дискретна математика", GradeTypes.Exam) => 20,
        _ => 10
    };

    private static IEnumerable<(string StudentEmail, string SubjectTitle, string ProfessorEmail, int Points, string Type, DateTime Date)> DemoRows()
    {
        var rows = new (string Student, string Subject, string Professor, int Points, string Type, int Y, int M, int D)[]
        {
            ("anton.ostapov@eger.ua", "Теорія баз даних", "professor@eger.ua", 10, GradeTypes.Attendance, 2025, 10, 2),
            ("anton.ostapov@eger.ua", "Теорія баз даних", "professor@eger.ua", 18, GradeTypes.Homework, 2025, 10, 16),
            ("anton.ostapov@eger.ua", "Теорія баз даних", "professor@eger.ua", 22, GradeTypes.Practical, 2025, 11, 6),
            ("anton.ostapov@eger.ua", "Теорія баз даних", "professor@eger.ua", 25, GradeTypes.Module, 2025, 12, 4),
            ("anton.ostapov@eger.ua", "Теорія баз даних", "professor@eger.ua", 20, GradeTypes.Exam, 2026, 1, 16),

            ("anton.ostapov@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 6, GradeTypes.Attendance, 2025, 10, 9),
            ("anton.ostapov@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 14, GradeTypes.Homework, 2025, 11, 13),
            ("anton.ostapov@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 16, GradeTypes.Practical, 2025, 12, 11),

            ("anton.ostapov@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 4, GradeTypes.Attendance, 2025, 10, 7),
            ("anton.ostapov@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 8, GradeTypes.Homework, 2025, 11, 4),
            ("anton.ostapov@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 10, GradeTypes.Practical, 2025, 12, 2),
            ("anton.ostapov@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 14, GradeTypes.Credit, 2026, 1, 20),

            ("daryna.kozak@eger.ua", "Теорія баз даних", "professor@eger.ua", 8, GradeTypes.Attendance, 2025, 10, 2),
            ("daryna.kozak@eger.ua", "Теорія баз даних", "professor@eger.ua", 12, GradeTypes.Homework, 2025, 10, 16),
            ("daryna.kozak@eger.ua", "Теорія баз даних", "professor@eger.ua", 16, GradeTypes.Practical, 2025, 11, 6),
            ("daryna.kozak@eger.ua", "Теорія баз даних", "professor@eger.ua", 20, GradeTypes.Module, 2025, 12, 4),
            ("daryna.kozak@eger.ua", "Теорія баз даних", "professor@eger.ua", 16, GradeTypes.Exam, 2026, 1, 16),

            ("maksym.lysenko@eger.ua", "Теорія баз даних", "professor@eger.ua", 6, GradeTypes.Attendance, 2025, 10, 2),
            ("maksym.lysenko@eger.ua", "Теорія баз даних", "professor@eger.ua", 10, GradeTypes.Homework, 2025, 10, 16),
            ("maksym.lysenko@eger.ua", "Теорія баз даних", "professor@eger.ua", 12, GradeTypes.Practical, 2025, 11, 6),
            ("maksym.lysenko@eger.ua", "Теорія баз даних", "professor@eger.ua", 8, GradeTypes.Module, 2025, 12, 4),
            ("maksym.lysenko@eger.ua", "Теорія баз даних", "professor@eger.ua", 8, GradeTypes.Exam, 2026, 1, 16),

            ("maksym.lysenko@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 5, GradeTypes.Attendance, 2025, 10, 7),
            ("maksym.lysenko@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 7, GradeTypes.Homework, 2025, 11, 4),
            ("maksym.lysenko@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 8, GradeTypes.Practical, 2025, 12, 2),
            ("maksym.lysenko@eger.ua", "Веб-технології", "ihor.bondarenko@eger.ua", 10, GradeTypes.Credit, 2026, 1, 20),

            ("oksana.kravchuk@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 4, GradeTypes.Attendance, 2025, 10, 9),
            ("oksana.kravchuk@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 6, GradeTypes.Homework, 2025, 11, 13),
            ("oksana.kravchuk@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 6, GradeTypes.Practical, 2025, 12, 11),

            ("sofia.tkachenko@eger.ua", "Алгоритми та структури даних", "ihor.bondarenko@eger.ua", 10, GradeTypes.Attendance, 2025, 10, 9),
            ("sofia.tkachenko@eger.ua", "Алгоритми та структури даних", "ihor.bondarenko@eger.ua", 16, GradeTypes.Homework, 2025, 11, 13),
            ("sofia.tkachenko@eger.ua", "Алгоритми та структури даних", "ihor.bondarenko@eger.ua", 18, GradeTypes.Practical, 2025, 11, 27),
            ("sofia.tkachenko@eger.ua", "Алгоритми та структури даних", "ihor.bondarenko@eger.ua", 16, GradeTypes.Module, 2025, 12, 11),
            ("sofia.tkachenko@eger.ua", "Алгоритми та структури даних", "ihor.bondarenko@eger.ua", 15, GradeTypes.Exam, 2026, 1, 20),

            ("pavlo.hrytsenko@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 8, GradeTypes.Attendance, 2025, 10, 9),
            ("pavlo.hrytsenko@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 10, GradeTypes.Homework, 2025, 11, 13),
            ("pavlo.hrytsenko@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 12, GradeTypes.Practical, 2025, 12, 11),
            ("pavlo.hrytsenko@eger.ua", "Алгоритми та структури даних", "professor@eger.ua", 12, GradeTypes.Exam, 2026, 1, 20),

            ("yulia.romaniuk@eger.ua", "Операційні системи", "andrii.melnyk@eger.ua", 12, GradeTypes.Attendance, 2025, 10, 3),
            ("yulia.romaniuk@eger.ua", "Операційні системи", "andrii.melnyk@eger.ua", 16, GradeTypes.Homework, 2025, 10, 24),
            ("yulia.romaniuk@eger.ua", "Операційні системи", "andrii.melnyk@eger.ua", 18, GradeTypes.Practical, 2025, 11, 14),
            ("yulia.romaniuk@eger.ua", "Операційні системи", "andrii.melnyk@eger.ua", 20, GradeTypes.Module, 2025, 12, 5),
            ("yulia.romaniuk@eger.ua", "Операційні системи", "andrii.melnyk@eger.ua", 18, GradeTypes.Exam, 2026, 1, 18),

            ("denys.savchuk@eger.ua", "Комп'ютерні мережі", "andrii.melnyk@eger.ua", 6, GradeTypes.Attendance, 2025, 10, 8),
            ("denys.savchuk@eger.ua", "Комп'ютерні мережі", "andrii.melnyk@eger.ua", 8, GradeTypes.Homework, 2025, 11, 5),
            ("denys.savchuk@eger.ua", "Комп'ютерні мережі", "andrii.melnyk@eger.ua", 10, GradeTypes.Practical, 2025, 12, 3),
            ("denys.savchuk@eger.ua", "Комп'ютерні мережі", "andrii.melnyk@eger.ua", 10, GradeTypes.Exam, 2026, 1, 23),

            ("kateryna.moroz@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 10, GradeTypes.Attendance, 2025, 10, 1),
            ("kateryna.moroz@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 14, GradeTypes.Homework, 2025, 10, 22),
            ("kateryna.moroz@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 16, GradeTypes.Practical, 2025, 11, 12),
            ("kateryna.moroz@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 18, GradeTypes.Module, 2025, 12, 10),
            ("kateryna.moroz@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 17, GradeTypes.Exam, 2026, 1, 9),

            ("nazar.polishchuk@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 8, GradeTypes.Attendance, 2025, 10, 1),
            ("nazar.polishchuk@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 8, GradeTypes.Homework, 2025, 10, 22),
            ("nazar.polishchuk@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 10, GradeTypes.Practical, 2025, 11, 12),
            ("nazar.polishchuk@eger.ua", "Дискретна математика", "maria.shevchenko@eger.ua", 8, GradeTypes.Exam, 2026, 1, 9)
        };

        foreach (var row in rows)
            yield return (row.Student, row.Subject, row.Professor, row.Points, row.Type, new DateTime(row.Y, row.M, row.D, 0, 0, 0, DateTimeKind.Utc));
    }

    private async Task<Professor> AddProfessorAsync(string email, string fullName, string department, string degree, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existingUser = await _users.GetByEmailAsync(normalized, ct);
        if (existingUser is not null)
        {
            var existingProfessor = await _professors.GetByUserIdAsync(existingUser.Id, ct);
            if (existingProfessor is not null)
                return existingProfessor;
        }

        var user = new User
        {
            Email = normalized,
            PasswordHash = _hasher.Hash(ProfessorPassword),
            Role = Roles.Professor,
            Is2FAEnabled = false
        };
        await _users.CreateAsync(user, ct);
        var professor = new Professor
        {
            UserId = user.Id,
            FullName = fullName,
            Department = department,
            AcademicDegree = degree
        };
        await _professors.CreateAsync(professor, ct);
        return professor;
    }

    private async Task<Student> AddStudentAsync(
        string email,
        string fullName,
        string group,
        string card,
        int year,
        CancellationToken ct)
    {
        var user = new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(StudentPassword),
            Role = Roles.Student,
            Is2FAEnabled = false
        };
        await _users.CreateAsync(user, ct);
        var student = new Student
        {
            UserId = user.Id,
            FullName = fullName,
            Group = group,
            StudentCardNumber = card,
            EnrollmentYear = year
        };
        await _students.CreateAsync(student, ct);
        return student;
    }

    private async Task<Subject> AddSubjectAsync(string title, int credits, List<string> professorIds, CancellationToken ct)
    {
        var subject = new Subject
        {
            Title = title,
            Credits = credits,
            ProfessorIds = professorIds
        };
        await _subjects.CreateAsync(subject, ct);
        return subject;
    }

    private async Task<int> AddGradeAsync(
        Student student,
        Subject subject,
        Professor professor,
        int value,
        string gradeType,
        DateTime date,
        string sessionId,
        CancellationToken ct)
    {
        await _grades.CreateAsync(new Grade
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            ProfessorId = professor.Id,
            GradeValue = value,
            GradeType = gradeType,
            Date = date,
            SessionId = sessionId
        }, ct);
        return 1;
    }
}
