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
    private readonly IPasswordHasher _hasher;

    public SeedService(
        IUserRepository users,
        IStudentRepository students,
        IProfessorRepository professors,
        ISubjectRepository subjects,
        IGradeRepository grades,
        IPasswordHasher hasher)
    {
        _users = users;
        _students = students;
        _professors = professors;
        _subjects = subjects;
        _grades = grades;
        _hasher = hasher;
    }

    public async Task<SeedResult> GenerateAsync(CancellationToken ct = default)
    {
        var existing = await _students.GetAllAsync(ct);
        if (existing.Any(s => s.FullName == DemoStudentName))
        {
            return new SeedResult
            {
                AlreadySeeded = true,
                Message = "Демонстраційні дані вже було згенеровано раніше",
                Students = existing.Count,
                Professors = (await _professors.GetAllAsync(ct)).Count,
                Subjects = (await _subjects.GetAllAsync(ct)).Count,
                Grades = (int)await _grades.CountAsync(ct)
            };
        }

        var kovalenko = await AddProfessorAsync("professor@eger.ua", "Коваленко Олена Ігорівна", "Кафедра комп'ютерних наук", "Доцент", ct);
        var melnyk = await AddProfessorAsync("andrii.melnyk@eger.ua", "Мельник Андрій Петрович", "Кафедра програмної інженерії", "Професор", ct);
        var shevchenko = await AddProfessorAsync("maria.shevchenko@eger.ua", "Шевченко Марія Василівна", "Кафедра інформаційних систем", "Кандидат технічних наук", ct);
        var bondarenko = await AddProfessorAsync("ihor.bondarenko@eger.ua", "Бондаренко Ігор Олександрович", "Кафедра комп'ютерних наук", "Доцент", ct);

        var databases = await AddSubjectAsync("Теорія баз даних", 5, [kovalenko.Id], ct);
        var algorithms = await AddSubjectAsync("Алгоритми та структури даних", 6, [kovalenko.Id, bondarenko.Id], ct);
        var web = await AddSubjectAsync("Веб-технології", 4, [bondarenko.Id], ct);
        var os = await AddSubjectAsync("Операційні системи", 5, [melnyk.Id], ct);
        var discrete = await AddSubjectAsync("Дискретна математика", 4, [shevchenko.Id], ct);
        var networks = await AddSubjectAsync("Комп'ютерні мережі", 4, [melnyk.Id], ct);
        var design = await AddSubjectAsync("Проєктування інформаційних систем", 5, [shevchenko.Id], ct);

        var anton = await AddStudentAsync("anton.ostapov@eger.ua", DemoStudentName, "КН-21", "KN-21015", 2021, ct);
        var daryna = await AddStudentAsync("daryna.kozak@eger.ua", "Козак Дарина Олегівна", "КН-21", "KN-21008", 2021, ct);
        var maksym = await AddStudentAsync("maksym.lysenko@eger.ua", "Лисенко Максим Сергійович", "КН-21", "KN-21022", 2021, ct);
        var oksana = await AddStudentAsync("oksana.kravchuk@eger.ua", "Кравчук Оксана Миколаївна", "КН-21", "KN-21031", 2021, ct);
        var sofia = await AddStudentAsync("sofia.tkachenko@eger.ua", "Ткаченко Софія Андріївна", "КН-22", "KN-22004", 2022, ct);
        var pavlo = await AddStudentAsync("pavlo.hrytsenko@eger.ua", "Гриценко Павло Іванович", "КН-22", "KN-22017", 2022, ct);
        var yulia = await AddStudentAsync("yulia.romaniuk@eger.ua", "Романюк Юлія Петрівна", "ПІ-21", "PI-21011", 2021, ct);
        var denys = await AddStudentAsync("denys.savchuk@eger.ua", "Савчук Денис Володимирович", "ПІ-21", "PI-21003", 2021, ct);
        var kateryna = await AddStudentAsync("kateryna.moroz@eger.ua", "Мороз Катерина Ігорівна", "ІС-21", "IS-21019", 2021, ct);
        var nazar = await AddStudentAsync("nazar.polishchuk@eger.ua", "Поліщук Назар Богданович", "ІС-21", "IS-21006", 2021, ct);

        var gradeCount = 0;
        gradeCount += await AddGradeAsync(anton, databases, kovalenko, 92, GradeTypes.Module, new DateTime(2025, 12, 12, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(anton, databases, kovalenko, 95, GradeTypes.Exam, new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(anton, algorithms, kovalenko, 88, GradeTypes.Exam, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(anton, web, bondarenko, 90, GradeTypes.Credit, new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(anton, discrete, shevchenko, 78, GradeTypes.Exam, new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(daryna, databases, kovalenko, 76, GradeTypes.Module, new DateTime(2025, 12, 12, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(daryna, databases, kovalenko, 81, GradeTypes.Exam, new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(daryna, algorithms, bondarenko, 84, GradeTypes.Exam, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(maksym, databases, kovalenko, 61, GradeTypes.Module, new DateTime(2025, 12, 12, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(maksym, databases, kovalenko, 64, GradeTypes.Exam, new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(maksym, web, bondarenko, 71, GradeTypes.Credit, new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(oksana, databases, kovalenko, 54, GradeTypes.Module, new DateTime(2025, 12, 12, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(oksana, databases, kovalenko, 58, GradeTypes.Exam, new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(oksana, algorithms, kovalenko, 67, GradeTypes.Exam, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(sofia, algorithms, bondarenko, 91, GradeTypes.Module, new DateTime(2026, 1, 14, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(sofia, web, bondarenko, 86, GradeTypes.Credit, new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(pavlo, algorithms, kovalenko, 73, GradeTypes.Exam, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(pavlo, web, bondarenko, 69, GradeTypes.Credit, new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(yulia, os, melnyk, 94, GradeTypes.Exam, new DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(yulia, networks, melnyk, 87, GradeTypes.Exam, new DateTime(2026, 1, 23, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(denys, os, melnyk, 62, GradeTypes.Exam, new DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(denys, networks, melnyk, 59, GradeTypes.Module, new DateTime(2025, 12, 18, 0, 0, 0, DateTimeKind.Utc), ct);

        gradeCount += await AddGradeAsync(kateryna, design, shevchenko, 90, GradeTypes.CourseWork, new DateTime(2025, 12, 28, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(kateryna, discrete, shevchenko, 83, GradeTypes.Exam, new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(nazar, design, shevchenko, 74, GradeTypes.CourseWork, new DateTime(2025, 12, 28, 0, 0, 0, DateTimeKind.Utc), ct);
        gradeCount += await AddGradeAsync(nazar, discrete, shevchenko, 66, GradeTypes.Exam, new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc), ct);

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
        CancellationToken ct)
    {
        await _grades.CreateAsync(new Grade
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            ProfessorId = professor.Id,
            GradeValue = value,
            GradeType = gradeType,
            Date = date
        }, ct);
        return 1;
    }
}
