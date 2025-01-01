using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Serilog;
using Serilog.Events;
using MongoDB.Bson;
using MongoDB.Driver;
using MineStatLib;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
string dbUser = Environment.GetEnvironmentVariable("MONGODB_USER");
string dbPassword = Environment.GetEnvironmentVariable("MONGODB_PASSWORD");
string dbIp = Environment.GetEnvironmentVariable("MONGODB_IP");
var client = new MongoClient($"mongodb://{dbUser}:{dbPassword}@{dbIp}");
var database = client.GetDatabase("minecraft-scanner");
var servers = database.GetCollection<BsonDocument>("servers-sharp");

var ipQueue = new ConcurrentQueue<string>();

string? unixsocketPath = args.SkipWhile(x => x != "-s").Skip(1).FirstOrDefault(); 
unixsocketPath ??= "/var/run/scanner.sock";
var unixsocketEndPoint = new UnixDomainSocketEndPoint(unixsocketPath);
if (File.Exists(unixsocketPath))
    File.Delete(unixsocketPath);

Task acceptIpProvidersTask = Task.Run(AcceptIpProvidersTask);
Task taskSchedulingTask = Task.Run(TaskSchedulingTask);
await acceptIpProvidersTask;
await taskSchedulingTask;

async Task AcceptIpProvidersTask()
{
    Socket socket = new Socket(
        AddressFamily.Unix,
        SocketType.Stream,
        ProtocolType.Unspecified);

    socket.Bind(unixsocketEndPoint);
    socket.Listen();

    Log.Information($"Begin accept conection.");
    while (true)
    {
        Socket clientSocket = await socket.AcceptAsync();
        var networkStream = new NetworkStream(clientSocket);
        var textStream = new StreamReader(networkStream);
        var ipProvider = new IpProvider(clientSocket, textStream);
        IpPullingTask(ipProvider).GetAwaiter();
        Log.Information($"New provider accepted: {clientSocket.RemoteEndPoint}");
    }

}

async Task IpPullingTask(IpProvider provider)
{
    while (provider.RawSocket.Connected)
    {
        TextReader reader = provider.Stream;

        string? ip = await reader.ReadLineAsync();

        if (ip is null ||
            !IPAddress.TryParse(ip, out _))
            break;

        Log.Information($"New ip puted to queue: {ip}");
        ipQueue.Enqueue(ip);
    }
    provider.Stream.Dispose();
    provider.RawSocket.Close();
    provider.RawSocket.Dispose();
}

async Task TaskSchedulingTask()
{
    var scanningTasks = new List<Task<MineStat>>();
    var resultHandlingTasks = new List<Task>();
    Log.Information($"Starting task scheduling.");
    while (true)
    {
        if (scanningTasks.Count < 50)
        {
            string? ip;
            if (ipQueue.TryDequeue(out ip))
            {
                Log.Information($"Start scann of {ip}");
                scanningTasks.Add(Task.Run<MineStat>(() => Scan(ip)));
            }
        }

        for (int i = 0; i < scanningTasks.Count; i++)
        {
            Task<MineStat> task = scanningTasks[i];

            if (task.IsCompleted)
            {
                scanningTasks.Remove(task);
                if (task.IsFaulted) {
                    Exception ex = task.Exception;
                    Log.Error($"Error ocured while scanning: {ex.Message};\n\tStackTrace={ex.StackTrace}");
                    continue;
                }
                MineStat result = await task;

                Log.Information($"Result of scanning {result.Address}: {result.ToJson()}");
                if (result.ServerUp)
                    resultHandlingTasks.Add(HandleScanningResult(result));
            }
        }

        for (int i = 0; i < resultHandlingTasks.Count; i++)
        {
            Task task = resultHandlingTasks[i];
            if (task.IsCompleted)
            {
                resultHandlingTasks.Remove(task);
                if (task.IsFaulted) {
                    Exception ex = task.Exception;
                    Log.Error($"Error ocured while handing result: {ex.Message};\n\tStackTrace={ex.StackTrace}");
                    continue;
                }
                await task;
            }
        }
        await Task.Delay(100);
    }
}

MineStat Scan(string ip)
{
    var stats = new MineStat(ip, 25565, timeout: 1);
    return stats;
}

async Task HandleScanningResult(MineStat result)
{
    Log.Information($"Start result handaling of {result.Address}.");
    BsonDocument document = result.ToBsonDocument();

    var filter = Builders<BsonDocument>.Filter
        .Eq(x => x["Address"], result.Address);

    BsonDocument? existingDocument = servers.Find(filter).FirstOrDefault();

    if (existingDocument is null)
    {
        Log.Information($"Creating new entry for {result.Address}.");
        await servers.InsertOneAsync(document);
        return;
    }

    Log.Information($"Updating entry for {result.Address}.");
    document = new BsonDocument { { "$set", document } };
    await servers.UpdateOneAsync(existingDocument, document);
}


record IpProvider(Socket RawSocket, StreamReader Stream);
