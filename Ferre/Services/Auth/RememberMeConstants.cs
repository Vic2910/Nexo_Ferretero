namespace Ferre.Services.Auth;

public static class RememberMeConstants
{
    public const string CookieName = "ferre.remember";
    public const string ProtectorPurpose = "Ferre.RememberSession.v1";
    public const string AccountsCookieName = "ferre.remember.accounts";
    public const string AccountsProtectorPurpose = "Ferre.RememberAccounts.v1";
    public const int MaxRememberedAccounts = 5;
}
