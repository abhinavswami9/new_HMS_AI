using System.Data;
using System.Data.Common;
using Careflow.Domain;
using Careflow.Infrastructure;
namespace Careflow.Api;

public static class AdministratorProvisioner
{
    public static async Task Run(SqlDatabase database)
    {
        var hospital = RequireGuid("CAREFLOW_HOSPITAL_ID"); var branch = RequireGuid("CAREFLOW_BRANCH_ID");
        var roleName = Environment.GetEnvironmentVariable("CAREFLOW_PROVISION_ROLE") ?? "Hospital Admin";
        if (!Permissions.Roles.ContainsKey(roleName)) throw new InvalidOperationException("Unsupported provisioning role.");
        var username = Require("CAREFLOW_ADMIN_USERNAME").ToLowerInvariant(); var name = Require("CAREFLOW_ADMIN_NAME"); var password = Require("CAREFLOW_ADMIN_PASSWORD");
        if (username.Length > 100 || name.Length > 160 || password.Length < 14 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            throw new InvalidOperationException("User requires bounded names and a password of at least 14 characters with letters, digits and a symbol.");
        await using var connection = await database.OpenAsync(CancellationToken.None);
        await using var tx = (DbTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        async Task<int> Execute(string sql, params (string Name, object Value)[] args)
        {
            await using var c = connection.CreateCommand(); c.Transaction = tx; c.CommandText = sql;
            foreach (var (key, value) in args) c.Parameters.Add(SqlDatabase.Parameter(c, key, value));
            return await c.ExecuteNonQueryAsync();
        }
        async Task<object?> Scalar(string sql, params (string Name, object Value)[] args)
        {
            await using var c = connection.CreateCommand(); c.Transaction = tx; c.CommandText = sql;
            foreach (var (key, value) in args) c.Parameters.Add(SqlDatabase.Parameter(c, key, value));
            return await c.ExecuteScalarAsync();
        }
        try
        {
            if (await Scalar("SELECT COUNT(1) FROM org.Branches WHERE Id=@b AND HospitalId=@h", ("b", branch), ("h", hospital)) is not 1) throw new InvalidOperationException("The specified hospital/branch does not exist.");
            if (await Scalar("SELECT COUNT(1) FROM iam.Users WHERE UserName=@u", ("u", username)) is not 0) throw new InvalidOperationException("Username already exists. Use a governed password-reset process.");
            var role = await Scalar("SELECT Id FROM iam.Roles WHERE HospitalId=@h AND Name=@name", ("h", hospital), ("name", roleName)) as Guid? ?? Guid.NewGuid();
            var user = Guid.NewGuid(); var membership = Guid.NewGuid();
            await Execute("IF NOT EXISTS(SELECT 1 FROM iam.Roles WHERE Id=@r) INSERT INTO iam.Roles(Id,HospitalId,Name,Description) VALUES(@r,@h,@name,'Configurable scoped role; clinical signing requires qualified membership')", ("r", role), ("h", hospital), ("name", roleName));
            foreach (var permission in Permissions.For(roleName)) await Execute("IF NOT EXISTS(SELECT 1 FROM iam.RolePermissions WHERE HospitalId=@h AND RoleId=@r AND PermissionKey=@p) INSERT INTO iam.RolePermissions(HospitalId,RoleId,PermissionKey) VALUES(@h,@r,@p)", ("h", hospital), ("r", role), ("p", permission));
            await Execute("INSERT INTO iam.Users(Id,HospitalId,UserName,DisplayName,PasswordHash,Enabled) VALUES(@id,@h,@u,@name,@hash,1)", ("id", user), ("h", hospital), ("u", username), ("name", name), ("hash", new IdentityPasswordService().Hash(password)));
            await Execute("INSERT INTO iam.Memberships(Id,HospitalId,BranchId,UserId,Active) VALUES(@id,@h,@b,@u,1)", ("id", membership), ("h", hospital), ("b", branch), ("u", user));
            await Execute("INSERT INTO iam.UserRoles(MembershipId,RoleId,HospitalId) VALUES(@m,@r,@h)", ("m", membership), ("r", role), ("h", hospital));
            if (roleName == "Doctor") { var practitioner = RequireGuid("CAREFLOW_PRACTITIONER_ID"); if (await Execute("UPDATE catalog.Practitioners SET UserId=@u WHERE Id=@p AND HospitalId=@h AND UserId IS NULL", ("u", user), ("p", practitioner), ("h", hospital)) != 1) throw new InvalidOperationException("Practitioner not found or already linked."); }
            await Execute("INSERT INTO audit.AuditEvents(Id,HospitalId,BranchId,ActorId,ActorName,Action,Entity,EntityId,Reason) VALUES(NEWID(),@h,@b,@actor,'Deployment operator','iam.admin.provisioned','User',@actor,'Explicit command-line provisioning')", ("h", hospital), ("b", branch), ("actor", user));
            await tx.CommitAsync(); Console.WriteLine($"User provisioned: {user} ({roleName}). Enable MFA through your enterprise identity provider before remote production access.");
        }
        catch { await tx.RollbackAsync(); throw; }
    }
    private static string Require(string key) => Environment.GetEnvironmentVariable(key) is string value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"{key} is required.");
    private static Guid RequireGuid(string key) => Guid.TryParse(Environment.GetEnvironmentVariable(key), out var value) && value != Guid.Empty ? value : throw new InvalidOperationException($"{key} must be a non-empty GUID.");
}
