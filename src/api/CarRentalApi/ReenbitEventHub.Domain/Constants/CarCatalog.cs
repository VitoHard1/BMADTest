namespace ReenbitEventHub.Domain.Constants;

public static class CarCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Cars = new Dictionary<string, string>
    {
        ["car-1"] = "Toyota Corolla",
        ["car-2"] = "VW Golf"
    };

    public static bool IsValidCarId(string carId) => Cars.ContainsKey(carId);

    public static string GetCarName(string carId) =>
        Cars.TryGetValue(carId, out var name) ? name : string.Empty;
}
