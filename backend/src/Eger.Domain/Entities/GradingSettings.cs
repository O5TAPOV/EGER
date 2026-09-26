namespace Eger.Domain.Entities;

public class GradingSettings
{
    public const string SingletonId = "000000000000000000000001";

    public string Id { get; set; } = SingletonId;

    public int PassThreshold { get; set; } = 50;

    public int CurrentMax { get; set; } = 80;

    public int FinalMax { get; set; } = 20;
}
