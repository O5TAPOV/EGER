using Eger.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Eger.Infrastructure.Mongo;

public sealed class MongoContext
{
    static MongoContext()
    {
        MongoMaps.Register();
    }

    public MongoContext(IConfiguration configuration)
    {
        var connectionString = configuration["Mongo:ConnectionString"];
        var database = configuration["Mongo:Database"];
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "mongodb://localhost:27017";
        if (string.IsNullOrWhiteSpace(database))
            database = "EgerDb";

        var client = new MongoClient(connectionString);
        Database = client.GetDatabase(database);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>("Users");
    public IMongoCollection<Student> Students => Database.GetCollection<Student>("Students");
    public IMongoCollection<Professor> Professors => Database.GetCollection<Professor>("Professors");
    public IMongoCollection<Subject> Subjects => Database.GetCollection<Subject>("Subjects");
    public IMongoCollection<Grade> Grades => Database.GetCollection<Grade>("Grades");
    public IMongoCollection<GradingSettings> GradingSettings => Database.GetCollection<GradingSettings>("GradingSettings");
}
