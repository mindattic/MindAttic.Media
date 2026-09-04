using MindAttic.Media;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class ThresholdSpillStreamTests
{
    [Test]
    public async Task StaysInMemoryBelowThreshold()
    {
        var overflowOpened = false;
        await using var sink = new ThresholdSpillStream(100, () => { overflowOpened = true; return new MemoryStream(); });

        await sink.WriteAsync(new byte[60]);
        await sink.WriteAsync(new byte[40]);

        Assert.Multiple(() =>
        {
            Assert.That(overflowOpened, Is.False);
            Assert.That(sink.Spilled, Is.False);
            Assert.That(sink.InlineBytes, Has.Length.EqualTo(100));
            Assert.That(sink.Length, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task SpillsOnceAndPreservesEveryByteInOrder()
    {
        var overflow = new MemoryStream();
        var opens = 0;
        var payload = Enumerable.Range(0, 500).Select(i => (byte)(i % 251)).ToArray();

        var sink = new ThresholdSpillStream(100, () => { opens++; return overflow; });
        await using (sink)
        {
            foreach (var chunk in payload.Chunk(37))
                await sink.WriteAsync(chunk);

            Assert.That(sink.Spilled, Is.True);
            Assert.That(sink.InlineBytes, Is.Null);
            Assert.That(sink.Length, Is.EqualTo(payload.Length));
        }

        Assert.That(opens, Is.EqualTo(1));
        Assert.That(overflow.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task CopyAndHashMatchesAOneShotHashOverTheSameBytes()
    {
        var payload = new byte[250_000];
        Random.Shared.NextBytes(payload);
        var destination = new MemoryStream();

        var (sha, length) = await MediaStreams.CopyAndHashAsync(new NonSeekableStream(payload), destination);

        Assert.Multiple(() =>
        {
            Assert.That(length, Is.EqualTo(payload.Length));
            Assert.That(sha, Is.EqualTo(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant()));
            Assert.That(destination.ToArray(), Is.EqualTo(payload));
        });
    }
}
