using System.Net;
using StackExchange.Redis;

namespace CachingProxy;
class CachingProxy
{
    // private static readonly ConcurrentDictionary<string, string> Cache = new(); //add redis
    private static IDatabase Cache = ConnectionMultiplexer.Connect("localhost:6379").GetDatabase();

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
            try{
                await Cache.ExecuteAsync("FLUSHDB");
                Console.WriteLine("Cache cleared.");
            }
            catch(Exception ex){
                Console.WriteLine("Something went wrong while cleaning the Cache.");
                return;
            }
        }

        string prefix = $"http://localhost:{port}/";
        
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        Console.WriteLine($"Caching proxy server started on {prefix}");

        while(true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            await Task.Run(() => HandleRequestAsync(context, originUrl));
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

    private static async Task HandleRequestAsync(HttpListenerContext context, string originUrl)
    {
        string requestPath = context.Request.Url.PathAndQuery;
        string requestUrl = $"{originUrl}{requestPath}";
        
        var resCached = await Cache.StringGetAsync(requestUrl);
        if (resCached.HasValue)
        {
            Console.WriteLine($"Cache HIT: {requestUrl}");

            await WriteResponseAsync(context, resCached, true);
            return;
        }

        Console.WriteLine($"Cache MISS: {requestUrl}");

        try
        {
            HttpRequestMessage forwardRequest = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), requestUrl);
            HttpResponseMessage forwardResponse = await client.SendAsync(forwardRequest);
            string responseBody = await forwardResponse.Content.ReadAsStringAsync();

            // Cache the response
            await Cache.StringSetAsync(requestUrl, responseBody, TimeSpan.FromSeconds(60));
            await WriteResponseAsync(context, responseBody, true, forwardResponse.Content.Headers.ContentType?.MediaType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error forwarding request: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponseAsync(context, "Internal server error", false);
        }

        return;
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, string responseBody, bool hitCache, string contentType = "application/json")
    {
        context.Response.ContentType = contentType;
        context.Response.Headers["X-Cache"] = hitCache ? "HIT" : "MISS";
        
        using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
        {
            await writer.WriteAsync(responseBody);
        }
        context.Response.Close();
    }
}
