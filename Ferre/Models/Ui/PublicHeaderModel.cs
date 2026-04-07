namespace Ferre.Models.Ui;

public sealed record NavItem(string Text, string Url);

public sealed record PublicHeaderModel(IReadOnlyList<NavItem> Items, string LogoUrl, string HomeUrl);
