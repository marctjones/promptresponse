using System.Text;
using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>
/// Coverage for the async serializer paths and the SerializationException constructors.
/// </summary>
public class AprJsonSerializerAsyncAndExceptionTests
{
    private readonly AprJsonSerializer _serializer = new();

    private static AprDocument MinimalTemplate() =>
        new()
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section> { new() { Id = "s1", Title = "S" } }
        };

    // ---- DeserializeAsync ----

    [Fact]
    public async Task DeserializeAsync_RoundTrip_FromMemoryStream_Works()
    {
        var doc = MinimalTemplate();
        var json = _serializer.Serialize(doc);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await _serializer.DeserializeAsync(stream);

        result.Metadata.Title.Should().Be("T");
        result.Sections.Should().ContainSingle();
    }

    [Fact]
    public async Task DeserializeAsync_NullStream_ThrowsArgumentNullException()
    {
        var act = async () => await _serializer.DeserializeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeserializeAsync_InvalidJson_ThrowsSerializationExceptionWithJsonInner()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not json {"));

        var act = async () => await _serializer.DeserializeAsync(stream);

        var ex = await act.Should().ThrowAsync<SerializationException>();
        ex.Which.Message.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public async Task DeserializeAsync_NullDocument_FromJsonLiteralNull_ThrowsSerializationException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("null"));

        var act = async () => await _serializer.DeserializeAsync(stream);

        var ex = await act.Should().ThrowAsync<SerializationException>();
        ex.Which.Message.Should().Contain("returned null");
    }

    [Fact]
    public async Task DeserializeAsync_RespectsCancellationToken()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_serializer.Serialize(MinimalTemplate())));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _serializer.DeserializeAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- SerializeAsync ----

    [Fact]
    public async Task SerializeAsync_RoundTrip_ToMemoryStream_ProducesValidJson()
    {
        var doc = MinimalTemplate();
        using var stream = new MemoryStream();

        await _serializer.SerializeAsync(doc, stream);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var written = await reader.ReadToEndAsync();
        written.Should().Contain("\"title\": \"T\"");
    }

    [Fact]
    public async Task SerializeAsync_NullDocument_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();

        var act = async () => await _serializer.SerializeAsync(null!, stream);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SerializeAsync_NullStream_ThrowsArgumentNullException()
    {
        var doc = MinimalTemplate();

        var act = async () => await _serializer.SerializeAsync(doc, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SerializeAsync_RespectsCancellationToken()
    {
        var doc = MinimalTemplate();
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _serializer.SerializeAsync(doc, stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SerializeAsync_FailingStream_WrapsInSerializationException()
    {
        var doc = MinimalTemplate();
        using var stream = new ThrowingStream();

        var act = async () => await _serializer.SerializeAsync(doc, stream);

        var ex = await act.Should().ThrowAsync<SerializationException>();
        ex.Which.Message.Should().Contain("Failed to serialize");
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() => throw new IOException("simulated I/O failure");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("simulated I/O failure");
    }

    // ---- SerializationException constructors ----

    [Fact]
    public void SerializationException_DefaultConstructor_ProducesUsableException()
    {
        var ex = new SerializationException();

        ex.Should().BeAssignableTo<Exception>();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void SerializationException_MessageOnlyConstructor_StoresMessage()
    {
        var ex = new SerializationException("boom");

        ex.Message.Should().Be("boom");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void SerializationException_MessageAndInner_StoresBoth()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new SerializationException("wrapper", inner);

        ex.Message.Should().Be("wrapper");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
