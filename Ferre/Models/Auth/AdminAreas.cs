namespace Ferre.Models.Auth;

public static class AdminAreas
{
    public const string Dashboard = "dashboard";
    public const string Products = "products";
    public const string Inventory = "inventory";
    public const string Users = "users";
    public const string Categories = "categories";
    public const string Permissions = "permissions";
    public const string Orders = "orders";
    public const string Support = "support";

    public static readonly string[] All =
    [
        Dashboard,
        Products,
        Inventory,
        Users,
        Categories,
        Permissions,
        Orders,
        Support
    ];
}
