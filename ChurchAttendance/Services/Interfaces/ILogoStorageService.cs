namespace ChurchAttendance.Services.Interfaces;

public interface ILogoStorageService
{
    /// <summary>Validates and stores an uploaded logo. Returns the public path (e.g. /uploads/logo/abc.png).</summary>
    Task<ServiceResult<string>> SaveAsync(IFormFile? file);

    Task<ServiceResult> RemoveAsync();
}
