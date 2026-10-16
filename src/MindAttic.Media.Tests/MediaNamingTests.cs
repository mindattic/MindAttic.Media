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
