namespace ChurchAttendance.Security;

public interface IAccessCodeHasher
{
    string Hash(string code);
    bool Verify(string hash, string code);
}
