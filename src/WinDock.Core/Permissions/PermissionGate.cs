using WinDock.Core;

namespace WinDock.Core.Permissions;

public static class PermissionIds
{
    public const string MediaRead = "media.read";
}

public interface IPermissionGate
{
    PrivacySettings Privacy { get; }

    IReadOnlyList<string> ActiveProfilePermissions { get; }

    /// <summary>Called by the host after each profile diff so widget subscriptions cannot bypass the AND of global privacy and profile permissions.</summary>
    void SetActiveProfile(IReadOnlyList<string> profilePermissions);

    void UpdatePrivacy(PrivacySettings privacy);

    /// <summary>Fail-closed: unknown permission IDs are denied.</summary>
    bool Allows(string permissionId);
}

public sealed class PermissionGate : IPermissionGate
{
    private IReadOnlyList<string> _profilePermissions = [];

    public PermissionGate(PrivacySettings privacy)
    {
        Privacy = privacy;
    }

    public PrivacySettings Privacy { get; private set; }

    public IReadOnlyList<string> ActiveProfilePermissions => _profilePermissions;

    public void SetActiveProfile(IReadOnlyList<string> profilePermissions)
    {
        _profilePermissions = profilePermissions.Count == 0
            ? []
            : profilePermissions.Distinct(StringComparer.Ordinal).ToArray();
    }

    public void UpdatePrivacy(PrivacySettings privacy)
    {
        Privacy = privacy;
    }

    public bool Allows(string permissionId)
    {
        if (string.Equals(permissionId, PermissionIds.MediaRead, StringComparison.Ordinal))
        {
            return Privacy.MediaRead
                   && _profilePermissions.Contains(PermissionIds.MediaRead, StringComparer.Ordinal);
        }

        if (string.Equals(permissionId, "network.details", StringComparison.Ordinal))
        {
            return Privacy.NetworkDetails;
        }

        if (string.Equals(permissionId, "process.names", StringComparison.Ordinal))
        {
            return Privacy.ProcessNames;
        }

        if (string.Equals(permissionId, "location.precise", StringComparison.Ordinal))
        {
            return Privacy.PreciseLocation;
        }

        if (string.Equals(permissionId, "telemetry.crash", StringComparison.Ordinal))
        {
            return Privacy.CrashReports;
        }

        return false;
    }
}
