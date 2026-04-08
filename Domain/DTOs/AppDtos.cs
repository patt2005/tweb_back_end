namespace Domain.DTOs;

public class AppResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RevenueCatId { get; set; } = string.Empty;
    public long? AppleSearchAdsId { get; set; }
}

public class CreateAppDto
{
    public string Name { get; set; } = string.Empty;
    public string RevenueCatId { get; set; } = string.Empty;
    public long? AppleSearchAdsId { get; set; }
}

public class UpdateAppDto
{
    public string Name { get; set; } = string.Empty;
    public string RevenueCatId { get; set; } = string.Empty;
    public long? AppleSearchAdsId { get; set; }
}
