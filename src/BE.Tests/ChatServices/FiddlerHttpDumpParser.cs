using System.Text;

namespace Chats.BE.Tests.ChatServices;

/// <summary>
/// 用于解析从Fiddler导出的HTTP会话dump文件
/// </summary>
public class FiddlerHttpDumpParser
{
    public record HttpDump(
        HttpRequest Request,
        HttpResponse Response
    );

    public record HttpRequest(
        string Method,
        string Url,
        string HttpVersion,
        Dictionary<string, string> Headers,
        string Body
    );

    public record HttpResponse(
        int StatusCode,
        string StatusText,
        string HttpVersion,
        Dictionary<string, string> Headers,
        List<string> Chunks
    )
    {
        /// <summary>
        /// 响应体（如果是chunked编码，则为所有chunk内容拼接；否则为原始响应体）
        /// </summary>
        public string Body => string.Concat(Chunks);
    };

    /// <summary>
    /// 解析Fiddler导出的dump文件
    /// </summary>
    public static HttpDump Parse(string content)
    {
        // 使用UTF-8字节来处理，因为chunked编码需要按字节计算长度
        var bytes = Encoding.UTF8.GetBytes(content);
        return Parse(bytes);
    }
    
    /// <summary>
    /// 从字节数组解析
    /// </summary>
    public static HttpDump Parse(byte[] bytes)
    {
        var content = Encoding.UTF8.GetString(bytes);
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        
        // 找到请求和响应的分界线
        int responseStartIndex = FindResponseStartIndex(lines);
        
        var request = ParseRequest(lines[..responseStartIndex]);
        var response = ParseResponse(bytes, lines, responseStartIndex);
        
        return new HttpDump(request, response);
    }

    /// <summary>
    /// 从文件解析
    /// </summary>
    public static HttpDump ParseFile(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return Parse(bytes);
    }

    private static int FindResponseStartIndex(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("HTTP/"))
            {
                return i;
            }
        }
        throw new InvalidOperationException("找不到HTTP响应的起始位置");
    }

    private static HttpRequest ParseRequest(string[] lines)
    {
        // 解析请求行: POST https://... HTTP/1.1
        var requestLine = lines[0].Split(' ', 3);
        var method = requestLine[0];
        var url = requestLine[1];
        var httpVersion = requestLine[2];

        // 解析请求头
        var headers = new Dictionary<string, string>();
        int bodyStartIndex = 1;
        
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                bodyStartIndex = i + 1;
                break;
            }
            
            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex > 0)
            {
                var headerName = lines[i][..colonIndex];
                var headerValue = lines[i][(colonIndex + 1)..].TrimStart();
                headers[headerName] = headerValue;
            }
        }

        // 解析请求体
        string body = string.Empty;
        if (bodyStartIndex < lines.Length)
        {
            body = string.Join("", lines[bodyStartIndex..]);
        }

        return new HttpRequest(method, url, httpVersion, headers, body);
    }

    private static HttpResponse ParseResponse(byte[] allBytes, string[] allLines, int responseStartLineIndex)
    {
        var lines = allLines[responseStartLineIndex..];
        
        // 解析状态行: HTTP/1.1 200 OK
        var statusLine = lines[0].Split(' ', 3);
        var httpVersion = statusLine[0];
        var statusCode = int.Parse(statusLine[1]);
        var statusText = statusLine.Length > 2 ? statusLine[2] : "";

        // 解析响应头
        var headers = new Dictionary<string, string>();
        int bodyStartLineIndex = 1;
        
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                bodyStartLineIndex = i + 1;
                break;
            }
            
            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex > 0)
            {
                var headerName = lines[i][..colonIndex];
                var headerValue = lines[i][(colonIndex + 1)..].TrimStart();
                headers[headerName] = headerValue;
            }
        }

        // 判断是否是chunked编码
        bool isChunked = headers.TryGetValue("Transfer-Encoding", out var transferEncoding) 
                         && transferEncoding.Equals("chunked", StringComparison.OrdinalIgnoreCase);

        List<string> chunks;
        
        if (isChunked)
        {
            // 找到响应体在字节数组中的起始位置
            // 计算从文件开头到响应体开始的行数
            int totalLinesBeforeBody = responseStartLineIndex + bodyStartLineIndex;
            int byteOffset = FindByteOffsetAfterLines(allBytes, totalLinesBeforeBody);
            
            chunks = ParseChunkedBody(allBytes, byteOffset);
        }
        else
        {
            // 非chunked编码，直接将剩余内容作为单个chunk
            var bodyLines = lines[bodyStartLineIndex..];
            var body = string.Join("\n", bodyLines);
            chunks = [body];
        }

        return new HttpResponse(statusCode, statusText, httpVersion, headers, chunks);
    }
    
    /// <summary>
    /// 找到指定行数之后的字节偏移位置
    /// </summary>
    private static int FindByteOffsetAfterLines(byte[] bytes, int lineCount)
    {
        int currentLine = 0;
        int i = 0;
        
        while (i < bytes.Length && currentLine < lineCount)
        {
            if (bytes[i] == '\n')
            {
                currentLine++;
            }
            i++;
        }
        
        return i;
    }
    
    /// <summary>
    /// 解析HTTP Chunked Transfer Encoding格式的响应体
    /// 格式: [chunk-size]\r\n[chunk-data]\r\n[chunk-size]\r\n[chunk-data]\r\n...0\r\n\r\n
    /// </summary>
    private static List<string> ParseChunkedBody(byte[] bytes, int startOffset)
    {
        var chunks = new List<string>();
        int position = startOffset;
        
        while (position < bytes.Length)
        {
            // 读取chunk大小行（十六进制）
            int lineEnd = FindLineEnd(bytes, position);
            if (lineEnd < 0) break;
            
            var sizeLineBytes = bytes[position..lineEnd];
            var sizeLine = Encoding.UTF8.GetString(sizeLineBytes).Trim();
            
            // 跳过空行
            if (string.IsNullOrEmpty(sizeLine))
            {
                position = SkipLineEnding(bytes, lineEnd);
                continue;
            }
            
            // 解析chunk大小
            if (!TryParseHexSize(sizeLine, out int chunkSize))
            {
                // 不是有效的chunk大小，可能已经到达末尾
                break;
            }
            
            // chunk大小为0表示结束
            if (chunkSize == 0)
            {
                break;
            }
            
            // 移动到chunk数据开始位置（跳过大小行的CRLF）
            position = SkipLineEnding(bytes, lineEnd);
            
            // 读取指定字节数的chunk数据
            if (position + chunkSize > bytes.Length)
            {
                // 数据不完整，读取剩余所有数据
                chunkSize = bytes.Length - position;
            }
            
            var chunkData = Encoding.UTF8.GetString(bytes, position, chunkSize);
            chunks.Add(chunkData);
            
            // 移动到chunk数据之后（chunk数据后面通常有CRLF）
            position += chunkSize;
            
            // 跳过chunk数据后的CRLF
            if (position < bytes.Length && bytes[position] == '\r') position++;
            if (position < bytes.Length && bytes[position] == '\n') position++;
        }
        
        return chunks;
    }
    
    /// <summary>
    /// 查找行结束位置（\r\n 或 \n 之前的位置）
    /// </summary>
    private static int FindLineEnd(byte[] bytes, int start)
    {
        for (int i = start; i < bytes.Length; i++)
        {
            if (bytes[i] == '\r' || bytes[i] == '\n')
            {
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// 跳过行结束符，返回下一行开始位置
    /// </summary>
    private static int SkipLineEnding(byte[] bytes, int lineEndPos)
    {
        int pos = lineEndPos;
        if (pos < bytes.Length && bytes[pos] == '\r') pos++;
        if (pos < bytes.Length && bytes[pos] == '\n') pos++;
        return pos;
    }
    
    /// <summary>
    /// 尝试解析十六进制的chunk大小
    /// </summary>
    private static bool TryParseHexSize(string line, out int size)
    {
        size = 0;
        var trimmed = line.Trim();
        
        // chunk大小行可能包含扩展信息（分号后面的部分），忽略它
        var semicolonIndex = trimmed.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            trimmed = trimmed[..semicolonIndex];
        }
        
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 8)
        {
            return false;
        }
        
        // 验证是否全是十六进制字符
        if (!trimmed.All(c => 
            (c >= '0' && c <= '9') || 
            (c >= 'a' && c <= 'f') || 
            (c >= 'A' && c <= 'F')))
        {
            return false;
        }
        
        try
        {
            size = Convert.ToInt32(trimmed, 16);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
