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
        /// 原始响应体（包含chunked编码）
        /// </summary>
        public string RawBody => string.Join("\n", Chunks);
        
        /// <summary>
        /// 去除chunked编码后的响应体
        /// </summary>
        public string DechunkedBody => string.Join("\n", Chunks.Where(line => !IsChunkSizeLine(line))).Trim();
        
        private static bool IsChunkSizeLine(string line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;
            
            // chunked编码的长度行是16进制数字
            return trimmed.Length <= 8 && trimmed.All(c => 
                (c >= '0' && c <= '9') || 
                (c >= 'a' && c <= 'f') || 
                (c >= 'A' && c <= 'F'));
        }
    };

    /// <summary>
    /// 解析Fiddler导出的dump文件
    /// </summary>
    public static HttpDump Parse(string content)
    {
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        
        // 找到请求和响应的分界线
        int responseStartIndex = FindResponseStartIndex(lines);
        
        var request = ParseRequest(lines[..responseStartIndex]);
        var response = ParseResponse(lines[responseStartIndex..]);
        
        return new HttpDump(request, response);
    }

    /// <summary>
    /// 从文件解析
    /// </summary>
    public static HttpDump ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Parse(content);
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

    private static HttpResponse ParseResponse(string[] lines)
    {
        // 解析状态行: HTTP/1.1 200 OK
        var statusLine = lines[0].Split(' ', 3);
        var httpVersion = statusLine[0];
        var statusCode = int.Parse(statusLine[1]);
        var statusText = statusLine[2];

        // 解析响应头
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

        // 解析响应体为Chunks列表
        var chunks = lines[bodyStartIndex..].ToList();

        return new HttpResponse(statusCode, statusText, httpVersion, headers, chunks);
    }
}
