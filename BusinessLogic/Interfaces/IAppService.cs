using Domain.DTOs;

namespace BusinessLogic.Interfaces;

public interface IAppService
{
    Task<IReadOnlyList<AppResponseDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<AppResponseDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<AppResponseDto?> CreateAsync(CreateAppDto dto, Guid userId, CancellationToken ct);
    Task<AppResponseDto?> UpdateAsync(Guid id, Guid userId, UpdateAppDto dto, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
