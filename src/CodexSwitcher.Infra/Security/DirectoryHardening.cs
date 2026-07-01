using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CodexSwitcher.Infra.Security;

/// <summary>
/// ACL restritiva na pasta do cofre: só o usuário atual, sem herança. Defesa em profundidade além
/// do DPAPI. Nunca exige admin. Ver BUSINESS_RULES.md §7. Best-effort: falha não é fatal.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DirectoryHardening
{
    public static bool TryRestrictToCurrentUser(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            using var identity = WindowsIdentity.GetCurrent();
            var user = identity.User;
            if (user is null)
                return false;

            var dirInfo = new DirectoryInfo(path);
            var security = new DirectorySecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                user,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IdentityNotMappedException or IOException)
        {
            return false;
        }
    }
}
