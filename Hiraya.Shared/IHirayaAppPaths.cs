namespace Hiraya.Shared;

public interface IHirayaAppPaths
{
    string ContentRootPath { get; }
    string DataDirectory { get; }
}

public sealed class DefaultHirayaAppPaths : IHirayaAppPaths
{
    public DefaultHirayaAppPaths(string contentRootPath, string? dataDirectory = null)
    {
        ContentRootPath = contentRootPath;
        DataDirectory = dataDirectory ?? Path.Combine(contentRootPath, ".data");
    }

    public string ContentRootPath { get; }
    public string DataDirectory { get; }
}
