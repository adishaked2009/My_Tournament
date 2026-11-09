using Model;

namespace View.Services;

public static class RoleHomePath
{
    public static string For(string roleName) => roleName switch
    {
        UserRoles.SuperAdmin => "/admin",
        UserRoles.Host => "/host",
        UserRoles.Player => "/player",
        UserRoles.Guest => "/guest",
        _ => "/no-permission"
    };
}
