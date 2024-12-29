
using System.Collections.Concurrent;
using System.Net;

namespace CachingProx;
class CachingProxy
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(); //add redis
    private static readonly HttpClient client = new();

    static async Task Main(string[] args)
    {
        string? originUrl;
        string port;

        port = GetValueFromArgs("port", args);

        if(port == null)
        {
            Console.WriteLine("Usage: caching-proxy --port <number> --origin <url>");
            return;
        }

        originUrl = GetValueFromArgs("origin", args);
        if(originUrl == null)
        {
            Console.WriteLine("Usage: --port <number> --origin <url> --clear-cache<optional> ");
            return;
        }

        var cache = GetValueFromArgs("clearCache", args);
        if (cache != null)
        {
            Cache.Clear();
            Console.WriteLine("Cache cleared.");
            return;
        }

        string prefix = $"http://localhost:{port}/";
        
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        Console.WriteLine($"Caching proxy server started on {prefix}");

        while(true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            await Task.Run(() => HandleRequest(context, originUrl));
        }
    }

    private static string? GetValueFromArgs(string key, string[] args){
        for (int i = 0; i < args.Length; i++)
        {
            if(args[i].Equals($"--" + key))
            {
                if(key.Equals("--clear-cache"))
                    return "clearCache";
                
                    return args[i + 1];
            }
        }

        return null;
    }

    private static async Task HandleRequest(HttpListenerContext context, string originUrl)
    {
        string requestPath = context.Request.Url.PathAndQuery;
        string requestUrl = $"{originUrl}{requestPath}";
        
        if (Cache.TryGetValue(requestUrl, out string cachedResponse))
        {
            Console.WriteLine($"Cache HIT: {requestUrl}");
            context.Response.Headers["X-Cache"] = "HIT";

            await WriteResponse(context, cachedResponse);
            return;
        }

        Console.WriteLine($"Cache MISS: {requestUrl}");
        context.Response.Headers["X-Cache"] = "MISS";

        try
        {
            HttpRequestMessage forwardRequest = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), requestUrl);
            HttpResponseMessage forwardResponse = await client.SendAsync(forwardRequest);
            string responseBody = await forwardResponse.Content.ReadAsStringAsync();

            // Cache the response
            Cache[requestUrl] = responseBody;
            await WriteResponse(context, responseBody, forwardResponse.Content.Headers.ContentType?.MediaType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error forwarding request: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponse(context, "Internal server error");
        }

        return;
    }

    private static async Task WriteResponse(HttpListenerContext context, string responseBody, string contentType = "application/json")
    {
        context.Response.ContentType = contentType;
        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
        {
            await writer.WriteAsync(responseBody);
        }
        context.Response.Close();
    }
}
