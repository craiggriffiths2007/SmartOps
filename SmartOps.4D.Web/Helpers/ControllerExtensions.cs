using Microsoft.AspNetCore.Mvc;

namespace SmartOps.Web.Helpers
{
    public static class ControllerExtensions
    {
        public static IActionResult ReplaceRedirect(
            this Controller controller,
            string url)
        {
            return new ReplaceRedirectResult(url);
        }

        public static IActionResult ReplaceRedirectToAction(
            this Controller controller,
            string actionName,
            object? routeValues = null)
        {
            var url = controller.Url.Action(
                actionName,
                routeValues);

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    $"Could not generate URL for action '{actionName}'.");

            return new ReplaceRedirectResult(url);
        }

        public static IActionResult ReplaceRedirectToAction(
            this Controller controller,
            string actionName,
            string controllerName,
            object? routeValues = null)
        {
            var url = controller.Url.Action(
                actionName,
                controllerName,
                routeValues);

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    $"Could not generate URL for '{controllerName}/{actionName}'.");

            return new ReplaceRedirectResult(url);
        }
    }
}