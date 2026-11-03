using MindAttic.Media;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class MediaNamingTests
{
    [TestCase("clip.mp4", "clip.mp4")]
    [TestCase(@"C:\videos\clip.mp4", "clip.mp4")]
    [TestCase("../../secrets.env", "secrets.env")]
    [TestCase("a/b/c.txt", "c.txt")]
    public void StripsDirectories(string input, string expected)
    {
        Assert.That(MediaNaming.SanitizeFileName(input), Is.EqualTo(expected));
    }

    [TestCase("na:me.txt", "na_me.txt")]
    [TestCase("bad*name?.png", "bad_name_.png")]
    public void ReplacesInvalidCharsWithUnderscore(string input, string expected)
    {
        Assert.That(MediaNaming.SanitizeFileName(input), Is.EqualTo(expected));
    }

    [TestCase(@"C:\videos\clip.mp4", "clip.mp4")]
    [TestCase(@"..\..\secrets.env", "secrets.env")]
    [TestCase("C:clip.mp4", "C_clip.mp4")]
    public void SanitizesWindowsPathsTheSameOnEveryOs(string input, string expected)
    {
        Assert.That(MediaNaming.SanitizeFileName(input), Is.EqualTo(expected));
    }

    [TestCase("clip.mp4")]
    [TestCase(@"C:\videos\clip.mp4")]
    [TestCase("bad*name?.png")]
    [TestCase("..")]
    public void LegacySanitizerLeavesPortableNamesUnchanged(string input)
    {
        var portable = MediaNaming.SanitizeFileName(input);
        Assert.That(MediaNaming.LegacySanitizeFileName(portable), Is.EqualTo(portable),
            "existing-row blob paths are rebuilt with the legacy sanitizer; new names must survive it");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("/")]
    [TestCase("a/b/")]
    [TestCase(@"C:\videos\")]
    public void ReturnsFileForEmptyWhitespaceOrDirOnlyInput(string input)
    {
        Assert.That(MediaNaming.SanitizeFileName(input), Is.EqualTo("file"));
    }
}
