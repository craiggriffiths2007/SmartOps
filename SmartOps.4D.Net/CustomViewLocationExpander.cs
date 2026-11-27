using Microsoft.AspNetCore.Mvc.Razor;
using System.Collections.Generic;

public class CustomViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        // Not needed for this scenario, but required by the interface
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        // {1} is the Controller name, {0} is the Action name
        var customLocations = new[]
        {
            "/Views/Items/{1}/{0}.cshtml",
            "/Views/Items/Shared/{0}.cshtml",
            "/Views/Vehicles/{1}/{0}.cshtml",
            "/Views/Vehicles/Shared/{0}.cshtml"
        };

        // Combine our new locations with the default ones
        return customLocations.Concat(viewLocations);
    }
}