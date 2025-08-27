using Chats.BE.Services.ImageInfo;
using System.Text;

namespace Chats.BE.Tests.Common;

public class PartialBufferedStreamTest
{
    [Fact]
    public void Should_Read_SeekedBytes_Correctly()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("This is a test stream.");
        using MemoryStream baseStream = new(data);
        int seekBytes = 10;
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, seekBytes);

        // Act
        byte[] seekedBytes = partialStream.SeekedBytes;

        // Assert
        byte[] expectedBytes = data.Take(seekBytes).ToArray();
        Assert.Equal(expectedBytes, seekedBytes);
    }

    [Fact]
    public void Should_Read_From_Stream_Correctly_After_SeekedBytes()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("This is a test stream.");
        using MemoryStream baseStream = new(data);
        int seekBytes = 10;
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, seekBytes);

        // Act
        byte[] buffer = new byte[data.Length];
        int bytesRead = partialStream.Read(buffer, 0, buffer.Length);

        // Assert
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data, buffer);
    }

    [Fact]
    public void Should_Handle_Stream_Shorter_Than_SeekBytes()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Short");
        using MemoryStream baseStream = new(data);
        int seekBytes = 10; // More than stream length
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, seekBytes);

        // Act
        byte[] seekedBytes = partialStream.SeekedBytes;

        // Assert
        Assert.Equal(data.Length, seekedBytes.Length);
        Assert.Equal(data, seekedBytes);

        // Reading should return data.Length bytes
        byte[] buffer = new byte[10];
        int bytesRead = partialStream.Read(buffer, 0, buffer.Length);
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data, buffer.Take(bytesRead).ToArray());
    }

    [Fact]
    public void Read_Should_Return_Correct_Bytes_When_Count_Is_Less()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        using MemoryStream baseStream = new(data);
        int seekBytes = 5;
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, seekBytes);

        // Act
        byte[] buffer = new byte[10];
        int bytesRead = partialStream.Read(buffer, 0, 10);

        // Assert
        Assert.Equal(10, bytesRead);
        Assert.Equal(data.Take(10).ToArray(), buffer.Take(bytesRead).ToArray());
    }

    [Fact]
    public void Read_Should_Handle_Count_Greater_Than_Stream_Length()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Hello World");
        using MemoryStream baseStream = new(data);
        int seekBytes = 5;
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, seekBytes);

        // Act
        byte[] buffer = new byte[100];
        int bytesRead = partialStream.Read(buffer, 0, 100);

        // Assert
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data, buffer.Take(bytesRead).ToArray());
    }

    [Fact]
    public void Should_Not_Support_SetLength()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Test data");
        using MemoryStream baseStream = new(data);
        PartialBufferedStream partialStream = new PartialBufferedStream(baseStream, 4);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => partialStream.SetLength(100));
    }

    // 自定义的非可查找流，用于测试基础流不可查找的情况
    private class NonSeekableStream : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _innerStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Stream does not support seeking.");
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }
    }
}