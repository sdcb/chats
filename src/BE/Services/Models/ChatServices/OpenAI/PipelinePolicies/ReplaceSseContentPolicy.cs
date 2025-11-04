using System.ClientModel.Primitives;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.ServerSentEvents;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

public class ReplaceSseContentPolicy(Func<byte[], byte[]> Replacer) : PipelinePolicy
{
    // 副构造函数：支持字符串级别的替换
    public ReplaceSseContentPolicy(Func<string, string> stringReplacer, Encoding? encoding = null) 
        : this(CreateByteReplacer(stringReplacer, encoding ?? Encoding.UTF8))
    {
    }

    // 副构造函数：支持简单的字符串搜索和替换
    public ReplaceSseContentPolicy(string searchText, string replaceText, Encoding? encoding = null) 
        : this(line => line.Replace(searchText, replaceText), encoding)
    {
    }

    private static Func<byte[], byte[]> CreateByteReplacer(Func<string, string> stringReplacer, Encoding encoding)
    {
        return bytes =>
        {
            string text = encoding.GetString(bytes);
            string replaced = stringReplacer(text);
            return encoding.GetBytes(replaced);
        };
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        // 同步封装异步
        ProcessAsync(message, pipeline, currentIndex).AsTask().GetAwaiter().GetResult();
    }

    public override async ValueTask ProcessAsync(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        // 先调用后续的 pipeline
        await ProcessNextAsync(message, pipeline, currentIndex)
            .ConfigureAwait(false);

        // 若响应流可读，则替换为自定义的流式替换流
        if (message.Response?.ContentStream != null && message.Response.ContentStream.CanRead)
        {
            // 用包装流替换原始流，实现流式边读边改
            message.Response.ContentStream = new ReplacingStream(
                message.Response.ContentStream,
                Replacer
            );
        }
    }

    class ReplacingStream(
        Stream innerStream,
        Func<byte[], byte[]> replacer) : Stream
    {
        private readonly Queue<byte> _pendingBuffer = new();  // 替换后待输出的数据队列

        // 使用 .NET 的 SSE 解析器逐事件解析底层流，避免粘包/拆包问题
        private IAsyncEnumerator<SseItem<byte[]>>? _sseEnumerator;

        #region 必要的属性和方法重写
        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => false; // 示例里不支持Seek
        public override bool CanWrite => false; // 不支持写
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        #endregion

        /// <summary>
        /// 同步读取示例（其实可以只实现异步 ReadAsync 即可，但演示完整）。
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // 为了不阻塞，直接调用异步版本并 .GetAwaiter().GetResult()
            return ReadAsync(buffer, offset, count, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// 流式异步读取核心逻辑：
        /// 1. 如果替换后的数据队列 _pendingBuffer 有数据，先吐出来。
        /// 2. 如果队列空了，再去底层流里 Read 一块数据进来，替换后放入队列。
        /// 3. 重复直到填满请求或底层流结束。
        /// </summary>
        public override async ValueTask<int> ReadAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken = default)
        {
            // 将调用方的 Memory<byte> 分解出数组、offset 等，以方便后面使用
            if (!MemoryMarshal.TryGetArray(destination, out ArraySegment<byte> segment))
                throw new InvalidOperationException("Buffer memory is not backed by an array");

            return await ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken)
                .ConfigureAwait(false);
        }

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            // 若调用方要求读0个字节，直接返回
            if (count == 0)
                return 0;

            int totalBytesRead = 0;

            // 懒初始化 SSE 解析器（使用本次 ReadAsync 的取消令牌），仅创建一次
            _sseEnumerator ??= SseParser
                .Create(innerStream, (string eventType, ReadOnlySpan<byte> bytes) => replacer(bytes.ToArray()))
                .EnumerateAsync()
                .GetAsyncEnumerator(cancellationToken);

            while (totalBytesRead < count)
            {
                // 如果待输出队列还有数据，先吐给调用方
                if (_pendingBuffer.Count > 0)
                {
                    buffer[offset + totalBytesRead] = _pendingBuffer.Dequeue();
                    totalBytesRead++;
                }
                else
                {
                    // 如果队列空了，则拉取下一个 SSE 事件并填充待输出队列
                    if (!await _sseEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        // 底层流已结束且无更多事件
                        break;
                    }

                    SseItem<byte[]> item = _sseEnumerator.Current;

                    using MemoryStream ms = new();
                    await SseFormatter.WriteAsync(
                        YieldOnceAsync(item, cancellationToken),
                        ms,
                        static (evt, writer) => WriteBytes(evt.Data, writer),
                        cancellationToken
                    ).ConfigureAwait(false);

                    byte[] formatted = ms.ToArray();
                    foreach (byte b in formatted)
                    {
                        _pendingBuffer.Enqueue(b);
                    }
                }
            }

            return totalBytesRead;
        }

        

        private static async IAsyncEnumerable<SseItem<byte[]>> YieldOnceAsync(
            SseItem<byte[]> item,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 立即产出一个事件；保持 async enumerable 形态以便复用 SseFormatter API
            yield return item;
            await Task.CompletedTask;
        }

        private static void WriteBytes(byte[] data, IBufferWriter<byte> writer)
        {
            Span<byte> span = writer.GetSpan(data.Length);
            data.AsSpan().CopyTo(span);
            writer.Advance(data.Length);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_sseEnumerator is not null)
                {
                    _sseEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    _sseEnumerator = null;
                }
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_sseEnumerator is not null)
            {
                await _sseEnumerator.DisposeAsync().ConfigureAwait(false);
                _sseEnumerator = null;
            }
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}