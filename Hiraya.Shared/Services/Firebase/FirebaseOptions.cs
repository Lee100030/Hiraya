namespace Hiraya.Shared.Services.Firebase;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = "hirayaplaycenter-df868";
    public string DatabaseUrl { get; set; } =
        "https://hirayaplaycenter-df868-default-rtdb.asia-southeast1.firebasedatabase.app";
    public string? ServiceAccountPath { get; set; }
    public string? ClientEmail { get; set; }
    public string? PrivateKey { get; set; }
    public bool UseFirebase { get; set; }
}
