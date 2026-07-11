using ShellKrypt.Infrastructure.CryptoTools;
using Xunit;

namespace ShellKrypt.Tests.CryptoTools;

public sealed class HashServiceTests
{
    private readonly HashService _service = new();

    [Fact]
    public void ComputeSha256_ReturnsExpectedLowercaseHex()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            _service.ComputeSha256("abc"));
    }

    [Fact]
    public void ComputeSha512_ReturnsExpectedLowercaseHex()
    {
        Assert.Equal(
            "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f",
            _service.ComputeSha512("abc"));
    }

    [Fact]
    public void ComputeHashes_ReturnEmpty_ForEmptyInput()
    {
        Assert.Empty(_service.ComputeSha256(""));
        Assert.Empty(_service.ComputeSha512(""));
    }
}
