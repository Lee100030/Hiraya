namespace Hiraya.Shared;

public class HirayaApiOptions
{
    public const string SectionName = "HirayaApi";

    public bool UseRemoteStore { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:5188";
}
