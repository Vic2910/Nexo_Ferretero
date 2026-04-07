using Ferre.Models.Auth;
using Ferre.Models.Ui;

namespace Ferre.Models.Catalog;

public sealed class CategoriesIndexViewModel
{
    public CategoriesIndexViewModel(
        IReadOnlyList<Category> categories,
        CategoryFormModel form,
        string? searchTerm,
        string sortOrder,
        int currentPage,
        int totalPages,
        int totalItems,
        int pageSize,
        IReadOnlyList<Product>? products = null,
        ProductFormModel? productForm = null,
        string? productSearchTerm = null,
        string productSortOrder = "az",
        int productCurrentPage = 1,
        int productTotalPages = 1,
        int productTotalItems = 0,
        int productPageSize = 10,
        IReadOnlyList<Category>? categoryOptions = null,
        IReadOnlyList<AdminUserViewModel>? users = null,
        IReadOnlyList<AdminNotificationViewModel>? notifications = null,
        IReadOnlyList<Product>? inventoryProducts = null,
        int clientUsersCount = 0,
        IReadOnlyDictionary<string, bool>? currentAdminPermissions = null,
        bool isSuperAdmin = false,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>? adminPermissionsByEmail = null)
    {
        Categories = categories;
        Form = form;
        SearchTerm = searchTerm;
        SortOrder = sortOrder;
        CurrentPage = currentPage;
        TotalPages = totalPages;
        TotalItems = totalItems;
        PageSize = pageSize;
        Products = products ?? Array.Empty<Product>();
        ProductForm = productForm ?? new ProductFormModel();
        ProductSearchTerm = productSearchTerm;
        ProductSortOrder = productSortOrder;
        ProductCurrentPage = productCurrentPage;
        ProductTotalPages = productTotalPages;
        ProductTotalItems = productTotalItems;
        ProductPageSize = productPageSize;
        CategoryOptions = categoryOptions ?? Array.Empty<Category>();
        Users = users ?? Array.Empty<AdminUserViewModel>();
        Notifications = notifications ?? Array.Empty<AdminNotificationViewModel>();
        InventoryProducts = inventoryProducts ?? Array.Empty<Product>();
        ClientUsersCount = Math.Max(0, clientUsersCount);
        CurrentAdminPermissions = currentAdminPermissions ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        IsSuperAdmin = isSuperAdmin;
        AdminPermissionsByEmail = adminPermissionsByEmail ?? new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Category> Categories { get; }

    public CategoryFormModel Form { get; }

    public string? SearchTerm { get; }

    public string SortOrder { get; }

    public int CurrentPage { get; }

    public int TotalPages { get; }

    public int TotalItems { get; }

    public int PageSize { get; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public IReadOnlyList<Product> Products { get; }

    public ProductFormModel ProductForm { get; }

    public string? ProductSearchTerm { get; }

    public string ProductSortOrder { get; }

    public int ProductCurrentPage { get; }

    public int ProductTotalPages { get; }

    public int ProductTotalItems { get; }

    public int ProductPageSize { get; }

    public IReadOnlyList<Category> CategoryOptions { get; }

    public IReadOnlyList<AdminUserViewModel> Users { get; }

    public IReadOnlyList<AdminNotificationViewModel> Notifications { get; }

    public IReadOnlyList<Product> InventoryProducts { get; }

    public int ClientUsersCount { get; }

    public IReadOnlyDictionary<string, bool> CurrentAdminPermissions { get; }

    public bool IsSuperAdmin { get; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>> AdminPermissionsByEmail { get; }

    public int UnreadNotificationsCount => Notifications.Count(x => !x.IsRead);

    public bool HasPreviousProductPage => ProductCurrentPage > 1;

    public bool HasNextProductPage => ProductCurrentPage < ProductTotalPages;
}
