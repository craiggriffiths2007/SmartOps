namespace SmartOps.Web.Models
{
    public static class SiteThemes
    {
        public static readonly IReadOnlyDictionary<string, string> All =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Midnight", "#03386C" },
                { "Ocean",    "#004682" },
                { "Garnet",   "#820000" },
                { "Forest",   "#008200" },
                { "Obsidian", "#020202" },
                { "Graphite", "#6E6E6E" },
                { "Lagoon",   "#008282" },
                { "Mocha",    "#654321" },
                { "Ochre",    "#826400" },
                { "Amethyst", "#800080" },
                { "Fuchsia",  "#C11C84" }
            };

        public static string GetColour(string? themeName)
        {
            if (!string.IsNullOrWhiteSpace(themeName) &&
                All.TryGetValue(themeName, out var colour))
            {
                return colour;
            }

            return All["Midnight"];
        }
    }
}
