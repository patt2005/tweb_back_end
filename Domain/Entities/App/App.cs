namespace Domain.Entities.App;

public class App
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User.User? User { get; set; }
    public string RevenueCatId { get; set; } = string.Empty;
    public long? AppleSearchAdsId { get; set; }
}