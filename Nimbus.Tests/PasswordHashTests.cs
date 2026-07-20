using Nimbus_Internet_Blocker.Utilities;
using Xunit;

namespace Nimbus.Tests;

public class PasswordHashTests
{
    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.True(PasswordHash.Verify("Correct-horse-9", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.False(PasswordHash.Verify("correct-horse-9", hash));   // case-sensitive
        Assert.False(PasswordHash.Verify("Wrong-horse-9", hash));
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        // Random per-hash salt ⇒ two hashes of the same input never collide,
        // yet both verify.
        var a = PasswordHash.Hash("Correct-horse-9");
        var b = PasswordHash.Hash("Correct-horse-9");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHash.Verify("Correct-horse-9", a));
        Assert.True(PasswordHash.Verify("Correct-horse-9", b));
    }

    [Fact]
    public void Hash_ProducesExpectedShape()
    {
        var parts = PasswordHash.Hash("Correct-horse-9").Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal("600000", parts[1]);
        Assert.NotEmpty(parts[2]);   // base64 salt
        Assert.NotEmpty(parts[3]);   // base64 subkey
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v1.600000.onlythreeparts")]
    [InlineData("v2.600000.c2FsdA==.c3Via2V5")]          // unknown version tag
    [InlineData("v1.notanumber.c2FsdA==.c3Via2V5")]      // unparseable iterations
    [InlineData("v1.600000.!!!notbase64!!!.c3Via2V5")]   // bad base64 salt
    // A value shaped like the old ASP.NET Identity blob — must be rejected, not throw.
    [InlineData("AQAAAAEAACcQAAAAEID0lengthyBase64Blob==")]
    public void Verify_MalformedOrLegacyStoredHash_ReturnsFalse(string? storedHash)
    {
        Assert.False(PasswordHash.Verify("anything", storedHash));
    }

    [Fact]
    public void Verify_EmptyAttempt_ReturnsFalse()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.False(PasswordHash.Verify("", hash));
        Assert.False(PasswordHash.Verify(null, hash));
    }

    [Fact]
    public void Verify_TamperedSubkey_Fails()
    {
        var parts = PasswordHash.Hash("Correct-horse-9").Split('.');
        var subkey = System.Convert.FromBase64String(parts[3]);
        subkey[0] ^= 0xFF;                                       // flip one byte
        parts[3] = System.Convert.ToBase64String(subkey);

        Assert.False(PasswordHash.Verify("Correct-horse-9", string.Join('.', parts)));
    }

    [Fact]
    public void Verify_HandlesUnicodeAndDashedRecoveryCode()
    {
        // Recovery codes are dashed (xxxxxx-xxxxxx-…); confirm dashes/unicode survive.
        const string code = "a1B!x9-Zç9$mn-Qw3#er-Ty7&ui";
        var hash = PasswordHash.Hash(code);
        Assert.True(PasswordHash.Verify(code, hash));
        Assert.False(PasswordHash.Verify(code.Replace('-', '_'), hash));
    }
}
