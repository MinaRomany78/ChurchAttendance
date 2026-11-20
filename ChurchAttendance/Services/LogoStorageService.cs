using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;

namespace ChurchAttendance.Services;

/// <summary>
/// Safe upload handling: size limit, extension whitelist, real content check (magic bytes),
/// server-generated file name (the client's file name is never used), fixed target folder.
/// SVG is deliberately not accepted because it can carry scripts.
/// </summary>
public class LogoStorageService : ILogoStorageService
{
    private const long MaxBytes = 1_000_000;
    private const string PublicFolder = "/uploads/logo/";

    private static readonly HashSet<string> AllowedExtensions = new() { ".png", ".jpg", ".jpeg", ".webp" };

    private readonly IWebHostEnvironment _env;
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;

    public LogoStorageService(IWebHostEnvironment env, ISettingsService settings, IAuditService audit)
    {
        _env = env;
        _settings = settings;
        _audit = audit;
    }

    public async Task<ServiceResult<string>> SaveAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return ServiceResult<string>.Fail("يرجى اختيار ملف الصورة.");
        if (file.Length > MaxBytes) return ServiceResult<string>.Fail("حجم الصورة يجب ألا يزيد عن 1 ميجابايت.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return ServiceResult<string>.Fail("الصيغ المسموحة: PNG أو JPG أو WEBP.");

        await using var input = file.OpenReadStream();
        var head = new byte[12];
        var read = await input.ReadAsync(head.AsMemory(0, head.Length));
        if (read < 12 || !LooksLikeImage(head))
            return ServiceResult<string>.Fail("الملف ليس صورة صالحة.");

        input.Position = 0;

        var folder = Path.Combine(_env.WebRootPath, "uploads", "logo");
        Directory.CreateDirectory(folder);

        var fileName = Guid.NewGuid().ToString("N") + (extension == ".jpeg" ? ".jpg" : extension);
        var fullPath = Path.Combine(folder, fileName);

        await using (var output = File.Create(fullPath))
        {
            await input.CopyToAsync(output);
        }

        var oldPath = await _settings.GetAsync(SettingKeys.LogoPath);
        var publicPath = PublicFolder + fileName;
        await _settings.SetAsync(SettingKeys.LogoPath, publicPath);
        DeleteOldFile(oldPath);

        await _audit.LogAsync("LogoUpdated", "Settings");
        return ServiceResult<string>.Ok(publicPath);
    }

    public async Task<ServiceResult> RemoveAsync()
    {
        var oldPath = await _settings.GetAsync(SettingKeys.LogoPath);
        await _settings.SetAsync(SettingKeys.LogoPath, string.Empty);
        DeleteOldFile(oldPath);

        await _audit.LogAsync("LogoRemoved", "Settings");
        return ServiceResult.Ok();
    }

    private void DeleteOldFile(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath) || !publicPath.StartsWith(PublicFolder, StringComparison.Ordinal)) return;

        // GetFileName strips any directory part, so this can never leave the logo folder.
        var fullPath = Path.Combine(_env.WebRootPath, "uploads", "logo", Path.GetFileName(publicPath));
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private static bool LooksLikeImage(byte[] h)
    {
        var png = h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
        var jpg = h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
        var webp = h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                   && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';
        return png || jpg || webp;
    }
}
