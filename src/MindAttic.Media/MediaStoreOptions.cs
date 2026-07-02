namespace MindAttic.Media;

public class MediaStoreOptions
{
    public string MediaRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "media");
    public long InlineThresholdBytes { get; set; } = 2L * 1024 * 1024;
}
