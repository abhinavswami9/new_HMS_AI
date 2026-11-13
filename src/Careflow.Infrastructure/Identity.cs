using Careflow.Application;
using Microsoft.AspNetCore.Identity;
namespace Careflow.Infrastructure;

public sealed class IdentityPasswordService : IPasswordService
{
    private readonly PasswordHasher<string> hasher = new(new Microsoft.Extensions.Options.OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions { IterationCount = 600_000, CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3 }));
    public string Hash(string password) => hasher.HashPassword("careflow", password);
    public bool Verify(string hash, string password, out bool needsRehash)
    {
        try
        {
            var result = hasher.VerifyHashedPassword("careflow", hash, password);
            needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
            return result != PasswordVerificationResult.Failed;
        }
        catch (Exception e) when (e is FormatException or ArgumentException) { needsRehash = false; return false; }
    }
}
