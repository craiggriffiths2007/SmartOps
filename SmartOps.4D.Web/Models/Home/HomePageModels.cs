namespace SmartOps.Web.Models.Home;

/// <summary>Eyebrow + heading + intro + tick list used by every feature section.</summary>
public sealed class FeatureCopyModel
{
    public string Eyebrow { get; init; } = "";
    public string Title { get; init; } = "";
    public string Intro { get; init; } = "";
    public IReadOnlyList<string> Bullets { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A row of phone screenshots that opens in the lightbox.
/// Images are expected at ~/img/{Folder}/Shot1.jpg, Shot2.jpg ... (one per caption).
/// </summary>
public sealed class PhoneGalleryModel
{
    /// <summary>Lightbox gallery key, e.g. "customer" or "field".</summary>
    public string Key { get; init; } = "";
    public string Folder { get; init; } = "";
    public string AppName { get; init; } = "";
    public IReadOnlyList<string> Captions { get; init; } = Array.Empty<string>();
    public string Extension { get; init; } = "jpg";
    public string? PlayStoreUrl { get; init; }
}

/// <summary>
/// A framed desktop screenshot. If GalleryKey is set the image opens in the lightbox;
/// GalleryImages (optional) lists every image in that gallery when there is more than the one shown.
/// Paths are app-relative, e.g. "~/img/screenshot1.png".
/// </summary>
public sealed class ShotFrameModel
{
    public string Src { get; init; } = "";
    public string Alt { get; init; } = "";
    public string? GalleryKey { get; init; }
    public IReadOnlyList<string> GalleryImages { get; init; } = Array.Empty<string>();
    public string OpenLabel { get; init; } = "View larger screenshot";
}
