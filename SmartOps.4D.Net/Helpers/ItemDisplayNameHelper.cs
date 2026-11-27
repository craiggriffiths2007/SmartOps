using System.Reflection;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Helpers
{
    public static class ItemDisplayNameHelper
    {
        public static string GetDisplayName(PropertyInfo property)
        {
            var display = property.GetCustomAttribute<DisplayAttribute>();

            if (!string.IsNullOrWhiteSpace(display?.GetName()))
                return display.GetName()!;

            var displayName =
                property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            // Legacy snake_case fields
            var name = property.Name.Replace("_", " ");

            // PascalCase fields
            name = Regex.Replace(
                name,
                "([a-z0-9])([A-Z])",
                "$1 $2");

            if (name.Length == 0)
                return property.Name;

            return char.ToUpper(name[0]) + name[1..];
        }
        public static string GetDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            name = name.Replace("_", " ");

            name = Regex.Replace(
                name,
                "([a-z0-9])([A-Z])",
                "$1 $2");

            return char.ToUpper(name[0]) + name[1..];
        }
    }


}