
# How to Run

1. Start your redis server;
2. Change the connection info on line [8](https://github.com/GabrielHNE/CachingProxy/blob/main/CachingProxy/Program.cs#L8) in the Program.cs file;
3. In your terminal run the `dotnet build` command to generate the `.exe` file;
4. With the `.exe` file you can start the program by calling it by its name `CachingProxy` and passing its arguments;
  a. Arguments/options to run:
    - `--port` is the port on which the proxy server will run;
    - `--origin` is the URL of the server to which the requests will be forwarded;
    - `--clear-cache` clears the cache.
  

## Example:
```batch
  caching-proxy --port 3000 --origin http://dummyjson.com
```

# How it works

The caching proxy server starts on a defined user `port` and forward requests to the `origin`.
If the user makes a request to http://localhost:<port\>/<some_resource>, the caching proxy server should forward the request to http://<origin\>/<some_resource> and
return the response along with headers and then cache the response.

### This project was inspired by this [Roadmap.sh project](https://roadmap.sh/projects/caching-server)
