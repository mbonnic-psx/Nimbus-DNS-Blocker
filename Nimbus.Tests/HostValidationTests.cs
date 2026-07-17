using Nimbus_Internet_Blocker.Utilities;
using Xunit;

namespace Nimbus.Tests;

public class HostValidationTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Example.COM  ", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("HTTP://EXAMPLE.COM", "example.com")]
    [InlineData("https://www.example.com/path?q=1#frag", "www.example.com")]
    [InlineData("example.com/deep/path", "example.com")]
    [InlineData("example.com:443", "example.com")]
    [InlineData("https://example.com:8080/x", "example.com")]
    [InlineData("example.com.", "example.com")]
    [InlineData("sub.domain.example.com", "sub.domain.example.com")]
    // Only http/https schemes are stripped, so "ftp://" is left in place and then
    // truncated at the first colon like a port would be — this pins current
    // behaviour, not an endorsement of it.
    [InlineData("ftp://example.com", "ftp")]
    public void NormalizeHost_ProducesExpectedResult(string? input, string expected)
    {
        var result = HostValidation.NormalizeHost(input);

        Assert.Equal(expected, result);
    }
}
