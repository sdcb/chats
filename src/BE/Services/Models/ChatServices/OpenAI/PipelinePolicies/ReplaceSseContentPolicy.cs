using System.ClientModel.Primitives;
using System.Runtime.InteropServices;
using System.Text;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

public class ReplaceSseContentPolicy(string SearchText, string ReplaceText) : PipelinePolicy
{
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
                SearchText,
                ReplaceText,
                Encoding.UTF8
            );
        }
    }

    class ReplacingStream(
        Stream innerStream,
        string searchText,
        string replaceText,
        Encoding encoding,
        int bufferSize = 4096) : Stream
    {
        private readonly byte[] _readBuffer = new byte[bufferSize];          // 每次从内层流中读取的临时缓冲
        private int _readBufferPos = 0;                   // 当前缓冲区中已经处理到的位置
        private int _readBufferLen = 0;                   // 当前缓冲区中实际可用数据长度

        private readonly Queue<byte> _pendingBuffer = new();  // 替换后待输出的数据队列

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
                    // 如果队列空了，就从内层流再读一块数据、替换
                    if (!await ReadAndReplaceOnceAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // 如果读不到更多数据了，就结束
                        break;
                    }
                }
            }

            return totalBytesRead;
        }

        /// <summary>
        /// 从 innerStream 中读取一块数据(_readBuffer)，执行字符串替换后，
        /// 把替换结果按照字节写入 _pendingBuffer。
        /// 
        /// 返回值表示是否真的读到新数据(true=读到新数据，false=流已结束)。
        /// </summary>
        private async Task<bool> ReadAndReplaceOnceAsync(CancellationToken cancellationToken)
        {
            if (_readBufferPos >= _readBufferLen)
            {
                // 当前缓冲用完，需要重新读
                _readBufferLen = await innerStream.ReadAsync(_readBuffer, 0, _readBuffer.Length, cancellationToken)
                                                   .ConfigureAwait(false);
                _readBufferPos = 0;
                if (_readBufferLen == 0)
                {
                    // 没有更多数据可读了
                    return false;
                }
            }

            // 把本次可读的所有字节拿来做替换（简化处理，未考虑跨缓冲区的分割）
            var chunkString = encoding.GetString(_readBuffer, _readBufferPos, _readBufferLen - _readBufferPos);
            _readBufferPos = _readBufferLen; // 模拟一次性读完

            // 做字符串替换
            var replacedString = chunkString.Replace(searchText, replaceText);

            // 将替换结果转回字节塞到队列中
            var replacedBytes = encoding.GetBytes(replacedString);
            foreach (var b in replacedBytes)
            {
                _pendingBuffer.Enqueue(b);
            }

            return true;
        }
    }
}