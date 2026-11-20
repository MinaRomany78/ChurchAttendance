using System.Security.Cryptography;
using ChurchAttendance.Services.Interfaces;
using QRCoder;

namespace ChurchAttendance.Services;

public class QrCodeService : IQrCodeService
{
    public string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public string ToPngDataUri(string content, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
