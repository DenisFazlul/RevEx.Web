namespace RevEx.Api.Authorization;

public static class RevExRoles
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Administrator = "administrator";
}

public static class RevExPolicies
{
    public const string EditContent = nameof(EditContent);
    public const string AdministerCatalog = nameof(AdministerCatalog);
}
