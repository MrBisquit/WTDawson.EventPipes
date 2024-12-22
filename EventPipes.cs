using System.IO.Pipes;

namespace WTDawson.EventPipes
{
    /// <summary>
    /// An EventPipes instance
    /// </summary>
    public class EventPipe : IDisposable
    {
        private NamedPipeServerStream server;
        private NamedPipeClientStream client;

        private StreamReader serverReader;
        private StreamReader clientReader;

        private StreamWriter serverWriter;
        private StreamWriter clientWriter;

        private Task primaryTask;
        private Task secondaryTask;

        private CancellationTokenSource primaryCTS;
        private CancellationTokenSource secondaryCTS;

        private Client _client;
        private Server _server;

        /// <summary>
        /// Creates an EventPipes instance.
        /// Swap the primary and secondary values on one end otherwise it will not work.
        /// </summary>
        /// <param name="primary">The name for the primary pipe instance</param>
        /// <param name="secondary">The name for the secondary pipe instance</param>
        public EventPipe(string primary, string secondary)
        {
            if(primary == secondary)
                throw new Exception("The primary cannot be the same as the secondary.");
            if(string.IsNullOrEmpty(primary) || string.IsNullOrEmpty(secondary))
                throw new Exception("Pipe names cannot be blank");

            server = new NamedPipeServerStream(primary);
            serverReader = new StreamReader(server);
            serverWriter = new StreamWriter(server);

            client = new NamedPipeClientStream(secondary);
            clientReader = new StreamReader(client);
            clientWriter = new StreamWriter(client);

            _client = new Client();
            _server = new Server();
        }

        /// <summary>
        /// Deconstructor for the EventPipes instance
        /// </summary>
        ~EventPipe()
        {
            Disconnect();
            Dispose();
        }

        /// <summary>
        /// Disposes the current EventPipes instance
        /// </summary>
        public void Dispose()
        {
            // Disconnect pipes
            Disconnect();

            // Close and dispose server reader
            serverReader.Close();
            serverReader.Dispose();

            // Close and dispose client reader
            clientReader.Close();
            clientReader.Dispose();

            // Close and dispose server writer
            serverWriter.Close();
            serverWriter.Dispose();

            // Close and dispose client writer
            clientWriter.Close();
            clientWriter.Dispose();

            // Cancel the tasks (kill the threads)
            primaryCTS.Cancel();
            secondaryCTS.Cancel();

            // Dispose the cancellation token sources
            primaryCTS.Dispose();
            secondaryCTS.Dispose();

            // Dispose the tasks (threads)
            primaryTask.Dispose();
            secondaryTask.Dispose();
        }

        /// <summary>
        /// Establish the connection between the pipes.
        /// </summary>
        /// <param name="timeout">The timeout</param>
        public void Connect(TimeSpan timeout = default)
        {
            if (timeout == default) client.Connect();
            else client.Connect(timeout);

            Initialise();
        }

        /// <summary>
        /// Establish the connection between the pipes.
        /// </summary>
        /// <param name="timeout">The timeout</param>
        public void Connect(int timeout)
        {
            if (timeout == default) client.Connect();
            else client.Connect(timeout);

            Initialise();
        }

        /// <summary>
        /// Establish the connection between the pipes.
        /// </summary>
        /// <param name="timeout">The timeout</param>
        /// <returns></returns>
        public async Task ConnectAsync(TimeSpan timeout = default, CancellationToken ct = default)
        {
            if (timeout == default) await client.ConnectAsync();
            else await client.ConnectAsync(ct);

            Initialise();
        }

        /// <summary>
        /// Establish the connection between the pipes.
        /// </summary>
        /// <param name="timeout">The timeout</param>
        /// <returns></returns>
        public async Task ConnectAsync(int timeout = default, CancellationToken ct = default)
        {
            if (timeout == default) await client.ConnectAsync();
            else await client.ConnectAsync(ct);

            Initialise();
        }

        /// <summary>
        /// Disconnects and closes the pipes
        /// </summary>
        public void Disconnect()
        {
            server.Disconnect();
            server.Close();
            client.Close();
        }

        /// <summary>
        /// Initialises the threads, readers and writers.
        /// </summary>
        private void Initialise()
        {
            primaryCTS = new CancellationTokenSource();
            secondaryCTS = new CancellationTokenSource();

            primaryTask = Task.Factory.StartNew(() =>
            {
                // Listen for any incoming messages
            }, primaryCTS.Token);

            secondaryTask = Task.Factory.StartNew(() =>
            {

            }, secondaryCTS.Token);
        }

        /// <summary>
        /// Reinitialises the threads, readers and writers.
        /// </summary>
        private void Reinitialise()
        {
            // Cancel the tasks (kill the threads)
            primaryCTS.Cancel();
            secondaryCTS.Cancel();

            // Dispose the cancellation token sources
            primaryCTS.Dispose();
            secondaryCTS.Dispose();

            // Dispose the tasks (threads)
            primaryTask.Dispose();
            secondaryTask.Dispose();

            // Initialise it again
            Initialise();
        }

        private class Event
        {

        }

        /// <summary>
        /// Add a callback to a specific event
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="callback">The callback</param>
        public void On(string name, Action<byte[]> callback)
        {

        }

        /// <summary>
        /// Removes all callbacks registered using the On function
        /// </summary>
        public void ClearCallbacks()
        {

        }

        private class Message
        {
            public string Name { get; set; } = string.Empty;
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        private string EncodeMessage(Message message)
        {
            return $"{message.Name},{ Convert.ToBase64String(message.Data)}";
        }

        private Message DecodeMessage(string data)
        {
            string[] split = data.Split(',');
            return new Message { Name = split[0], Data = Convert.FromBase64String(split[1]) };
        }

        public void Send(string name, byte[] data)
        {
            
        }

        public async Task SendAsync(string name, byte[] data)
        {

        }
    }
}