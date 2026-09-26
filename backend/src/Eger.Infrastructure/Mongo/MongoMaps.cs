using Eger.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Eger.Infrastructure.Mongo;

public static class MongoMaps
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("egerCamel", pack, _ => true);

        Register<User>();
        Register<Student>();
        Register<Professor>();
        Register<Subject>();
        Register<Grade>();
        _registered = true;
    }

    private static void Register<T>() where T : class
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
            return;

        BsonClassMap.RegisterClassMap<T>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.IdMemberMap?.SetSerializer(new StringSerializer(BsonType.ObjectId));
        });
    }
}
