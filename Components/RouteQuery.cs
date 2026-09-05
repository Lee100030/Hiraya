using Microsoft.AspNetCore.Components;

namespace Hiraya.Components;

public static class RouteQuery
{
    public static string Get(NavigationManager nav, string key)
    {
        var query = nav.ToAbsoluteUri(nav.Uri).Query;
        if (string.IsNullOrEmpty(query))
            return "";
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 0)
                continue;
            if (!string.Equals(Uri.UnescapeDataString(pair[0]), key, StringComparison.OrdinalIgnoreCase))
                continue;
            return pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "1";
        }
        return "";
    }
}
