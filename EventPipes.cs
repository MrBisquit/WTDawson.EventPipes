using System.IO.Pipes;
using System.Linq.Expressions;

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
        }

        /// <summary>
        /// Deconstructor for the EventPipes instance
        /// </summary>
        ~EventPipe()
        {
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

            if (IsConnected())
                InvokeSystemCallback("connected");

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

            if (IsConnected())
                InvokeSystemCallback("connected");

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

            if (IsConnected())
                InvokeSystemCallback("connected");

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

            if(IsConnected())
                InvokeSystemCallback("connected");

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
            InvokeSystemCallback("disconnected");
        }

        /// <summary>
        /// Returns if the pipes are connected.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            return server.IsConnected && client.IsConnected;
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
                while (true)
                {
                    server.WaitForConnection();
                }
            }, primaryCTS.Token);

            secondaryTask = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    string? content = clientReader.ReadLine();
                    if (content != null)
                    {
                        Message message = DecodeMessage(content);

                        for (int i = 0; i < callbacks.Count; i++)
                        {
                            if(callbacks[i].Key == message.Name)
                            {
                                callbacks[i].Value.ForEach((EventCallback callback) =>
                                {
                                    callback.callback(message.Data);
                                });
                            }
                        }
                    }
                }
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

        List<KeyValuePair<string, List<EventCallback>>> callbacks = new List<KeyValuePair<string, List<EventCallback>>>();
        KeyValuePair<string, List<SystemCallback>>[] systemCallbacks =
        {
            new("connected", new List<SystemCallback>()),
            new("disconnected", new List<SystemCallback>())
        };

        /// <summary>
        /// Invoke a system callback
        /// </summary>
        /// <param name="name">The callback name</param>
        private void InvokeSystemCallback(string name)
        {
            for (int i = 0; i < systemCallbacks.Length; i++)
            {
                if(systemCallbacks[i].Key == name)
                {
                    for (int j = 0; j < systemCallbacks[i].Value.Count; j++)
                    {
                        systemCallbacks[i].Value[j].callback();
                    }
                }
            }
        }

        private class EventCallback
        {
            public Action<byte[]> callback { get; set; }
        }

        private class SystemCallback
        {
            public Action callback { get; set; }
        }

        /// <summary>
        /// Add a callback to a specific event (Will receive over the secondary pipe)
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="callback">The callback</param>
        public void On(string name, Action<byte[]> callback)
        {
            for (int i = 0; i < callbacks.Count; i++)
            {
                if (callbacks[i].Key == name)
                {
                    callbacks[i].Value.Add(new EventCallback() { callback = callback });
                    return;
                }
            }
            callbacks.Add(new KeyValuePair<string, List<EventCallback>>(name, new List<EventCallback>() { new EventCallback() { callback = callback } }));
            return;
        }

        /// <summary>
        /// Add a callback to a system event, available events: connected, disconnected.
        /// </summary>
        /// <param name="name">The name of the system event</param>
        /// <param name="callback">The callback</param>
        public void Once(string name, Action callback)
        {
            for (int i = 0; i < systemCallbacks.Length; i++)
            {
                if(systemCallbacks[i].Key == name)
                {
                    systemCallbacks[i].Value.Add(new SystemCallback() { callback = callback });
                    return;
                }
            }
            throw new Exception("Invalid system callback, check documentation for details");
        }

        /// <summary>
        /// Removes all callbacks registered using the On function
        /// </summary>
        public void ClearCallbacks()
        {
            callbacks.Clear();
        }

        /// <summary>
        /// Removes all callbacks registered using the Once function
        /// </summary>
        public void ClearSystemCallbacks()
        {
            for (int i = 0; i < systemCallbacks.Length; i++)
            {
                systemCallbacks[i].Value.Clear();
            }
        }

        private class Message
        {
            public string Name { get; set; } = string.Empty;
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        /// <summary>
        /// Encodes a message
        /// </summary>
        /// <param name="message">The message object</param>
        /// <returns>The encoded message</returns>
        private string EncodeMessage(Message message)
        {
            return $"{message.Name},{ Convert.ToBase64String(message.Data)}";
        }

        /// <summary>
        /// Decodes a message
        /// </summary>
        /// <param name="data">The encoded message</param>
        /// <returns>The decoded message</returns>
        private Message DecodeMessage(string data)
        {
            string[] split = data.Split(',');
            return new Message { Name = split[0], Data = Convert.FromBase64String(split[1]) };
        }

        /// <summary>
        /// Send a message over the primary pipe (server)
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="data">The data</param>
        public void Send(string name, byte[] data)
        {
            string message = EncodeMessage(new Message() { Name = name, Data = data });
            serverWriter.WriteLine(message);
            serverWriter.Flush();
        }

        /// <summary>
        /// Send a message over the primary pipe (server)
        /// </summary>
        /// <param name="name">The name of the event</param>
        /// <param name="data">The data</param>
        /// <returns></returns>
        public async Task SendAsync(string name, byte[] data)
        {
            string message = EncodeMessage(new Message() { Name = name, Data = data });
            await serverWriter.WriteLineAsync(message);
            await serverWriter.FlushAsync();
        }
    }
}