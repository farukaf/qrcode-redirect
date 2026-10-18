using System.Security.Cryptography;

namespace QrRedirect.Services;

public class StringGeneratorService
{
    public string Generate(int length) =>
        RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", length);
}
