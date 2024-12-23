using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WTDawson.EventPipes
{
    /// <summary>
    /// A one-way EventPipe
    /// </summary>
    public static class OneWayEventPipe
    {
        public class Client : IDisposable
        {
            private NamedPipeClientStream client;
            private StreamReader clientReader;
            private Task task;
            private CancellationTokenSource CTS;

            public Client(string pipeName)
            {
                client = new NamedPipeClientStream(pipeName);
                clientReader = new StreamReader(client);
            }

            ~Client()
            {
                Dispose();
            }

            public void Dispose()
            {
                Disconnect();

                // Close and dispose client reader
                clientReader.Close();
                clientReader.Dispose();

                // Cancel the task (kill the thread)
                CTS.Cancel();

                // Dispose the cancellation token source
                CTS.Dispose();

                // Dispose the task (thread)
                task.Dispose();
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

                if (IsConnected())
                    InvokeSystemCallback("connected");

                Initialise();
            }

            /// <summary>
            /// Disconnects and closes the pipes
            /// </summary>
            public void Disconnect()
            {
                client.Close();
                InvokeSystemCallback("disconnected");
            }

            /// <summary>
            /// Initialises the threads, readers and writers.
            /// </summary>
            private void Initialise()
            {
                CTS = new CancellationTokenSource();

                task = Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        string? content = clientReader.ReadLine();
                        if (content != null)
                        {
                            Message message = Message.DecodeMessage(content);

                            for (int i = 0; i < callbacks.Count; i++)
                            {
                                if (callbacks[i].Key == message.Name)
                                {
                                    callbacks[i].Value.ForEach((EventCallback callback) =>
                                    {
                                        callback.callback(message.Data);
                                    });
                                }
                            }
                        }
                    }
                }, CTS.Token);
            }

            /// <summary>
            /// Returns if the pipes are connected.
            /// </summary>
            /// <returns></returns>
            public bool IsConnected()
            {
                return client.IsConnected;
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
                    if (systemCallbacks[i].Key == name)
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
                    if (systemCallbacks[i].Key == name)
                    {
                        systemCallbacks[i].Value.Add(new SystemCallback() { callback = callback });
                        return;
                    }
                }
                throw new Exception("Invalid system callback, check documentation for details");
            }
        }

        public class Server : IDisposable
        {
            private NamedPipeServerStream server;
            private StreamWriter serverWriter;
            private Task task;
            private CancellationTokenSource CTS;

            public Server(string pipeName)
            {
                server = new NamedPipeServerStream(pipeName);
                serverWriter = new StreamWriter(server);
            }

            public void OpenConnection()
            {
                CTS = new CancellationTokenSource();

                task = Task.Factory.StartNew(() =>
                {
                    server.WaitForConnection();
                    InvokeSystemCallback("connected");
                }, CTS.Token);
            }

            ~Server()
            {
                Dispose();
            }

            public void Dispose()
            {
                // Disconnect pipes
                Disconnect();

                // Close and dispose server writer
                serverWriter.Close();
                serverWriter.Dispose();

                // Cancel the task (kill the thread)
                CTS.Cancel();

                // Dispose the cancellation token source
                CTS.Dispose();

                // Dispose the task (thread)
                task.Dispose();
            }

            /// <summary>
            /// Disconnects and closes the pipes
            /// </summary>
            public void Disconnect()
            {
                server.Disconnect();
                server.Close();
                InvokeSystemCallback("disconnected");
            }

            /// <summary>
            /// Returns if the pipes are connected.
            /// </summary>
            /// <returns></returns>
            public bool IsConnected()
            {
                return server.IsConnected;
            }

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
                    if (systemCallbacks[i].Key == name)
                    {
                        for (int j = 0; j < systemCallbacks[i].Value.Count; j++)
                        {
                            systemCallbacks[i].Value[j].callback();
                        }
                    }
                }
            }

            private class SystemCallback
            {
                public Action callback { get; set; }
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
                    if (systemCallbacks[i].Key == name)
                    {
                        systemCallbacks[i].Value.Add(new SystemCallback() { callback = callback });
                        return;
                    }
                }
                throw new Exception("Invalid system callback, check documentation for details");
            }

            /// <summary>
            /// Send a message over the primary pipe (server)
            /// </summary>
            /// <param name="name">The name of the event</param>
            /// <param name="data">The data</param>
            public void Send(string name, byte[] data)
            {
                string message = Message.EncodeMessage(new Message() { Name = name, Data = data });
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
                string message = Message.EncodeMessage(new Message() { Name = name, Data = data });
                await serverWriter.WriteLineAsync(message);
                await serverWriter.FlushAsync();
            }
        }

        public class Message
        {
            public string Name { get; set; } = string.Empty;
            public byte[] Data { get; set; } = Array.Empty<byte>();

            /// <summary>
            /// Encodes a message
            /// </summary>
            /// <param name="message">The message object</param>
            /// <returns>The encoded message</returns>
            public static string EncodeMessage(Message message)
            {
                return $"{message.Name},{Convert.ToBase64String(message.Data)}";
            }

            /// <summary>
            /// Decodes a message
            /// </summary>
            /// <param name="data">The encoded message</param>
            /// <returns>The decoded message</returns>
            public static Message DecodeMessage(string data)
            {
                string[] split = data.Split(',');
                return new Message { Name = split[0], Data = Convert.FromBase64String(split[1]) };
            }
        }
    }
}