namespace SmartOps.Shared.Models;

public static class AssemblyWorkflowQuestions
{
    public static bool IsAssembly(string key) => key.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase);
}
