namespace BKPos.Licensing;

public interface ILicenseRevocationChecker
{
    bool IsRevoked(string licenseId);
}
