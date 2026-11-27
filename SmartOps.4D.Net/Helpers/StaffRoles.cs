namespace SmartOps.Web.Helpers;

public static class StaffRoles
{
    public const string Admin = "Admin";
    public const string Users = "Users";
    public const string Diary = "Diary";
    public const string Branch = "Branch";
    public static readonly string[] All = [Admin, Users, Diary, Branch];
    public static readonly string[] Defaults = [Users, Diary, Branch];
    public const string ManageUsers = "Admin,Users,SuperAdmin";
    public const string ManageDiary = "Admin,Diary";
    public const string ManageBranch = "Admin,Branch";
}
