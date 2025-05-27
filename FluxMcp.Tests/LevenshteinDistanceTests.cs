using FluxMcp.Tools;

namespace FluxMcp.Tests;

[TestClass]
public class LevenshteinDistanceTests
{
    [TestMethod]
    public void IdenticalStringsDistanceIsZero()
    {
        var result = NodeToolHelpers.LevenshteinDistance("test".AsSpan(), "test".AsSpan());
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void HandlesEmptyStrings()
    {
        Assert.AreEqual(0, NodeToolHelpers.LevenshteinDistance(string.Empty.AsSpan(), string.Empty.AsSpan()));
        Assert.AreEqual(3, NodeToolHelpers.LevenshteinDistance("abc".AsSpan(), string.Empty.AsSpan()));
        Assert.AreEqual(3, NodeToolHelpers.LevenshteinDistance(string.Empty.AsSpan(), "abc".AsSpan()));
    }

    [TestMethod]
    public void CalculatesTypicalDistance()
    {
        var result = NodeToolHelpers.LevenshteinDistance("kitten".AsSpan(), "sitting".AsSpan());
        Assert.AreEqual(3, result);
    }
}
