namespace ChurchAttendance.Services.Interfaces;

public interface IQrCodeService
{
    /// <summary>Creates a new opaque random token (32 hex chars) to identify a student in QR codes / barcodes.</summary>
    string GenerateToken();

    /// <summary>Renders the content as a PNG QR code and returns it as a data URI for use in an img tag.</summary>
    string ToPngDataUri(string content, int pixelsPerModule = 10);
}
