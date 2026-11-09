using System.Collections.Concurrent;

namespace SmartOps.Web;

public static partial class EndpointMappings
{
    // Retained for the unregistered media mappings while their migration is verified.
    // The original declaration is preserved in LegacyReference/Endpoints/EndpointMappings.cs.
    static ConcurrentDictionary<string, object> _uploadLocks = new();
}
