using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace maimai_rpc
{
    /// <summary>
    /// Pure C# lightweight Discord RPC client connecting directly over local Windows Named Pipes.
    /// Runs all pipe connection, read, and write operations completely on a dedicated background
    /// worker thread so the game main thread is never blocked or frozen.
    /// Uses non-blocking PeekNamedPipe checks to eliminate kernel I/O deadlocks in Mono and .NET 3.5.
    /// </summary>
    public class DiscordIpcClient : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PeekNamedPipe(
            IntPtr hNamedPipe,
            byte[] lpBuffer,
            uint nBufferSize,
            IntPtr lpBytesRead,
            out uint lpTotalBytesAvail,
            IntPtr lpBytesLeftThisMessage);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentProcessId();

        private static int GetProcessId()
        {
            try
            {
                return GetCurrentProcessId();
            }
            catch
            {
                try
                {
                    return System.Diagnostics.Process.GetCurrentProcess().Id;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string ApplicationId { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsReady { get; private set; }
        public DiscordUser CurrentUser { get; private set; }

        public event Action<DiscordUser> OnReady;
        public event Action<int, string> OnClose;
        public event Action<string> OnError;
        public event Action OnDisconnected;

        public Action<string> LogInfo { get; set; }
        public Action<string> LogWarn { get; set; }
        public Action<string> LogError { get; set; }

        private NamedPipeClientStream _stream;
        private Thread _workerThread;
        private readonly object _activityLock = new object();
        private readonly AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private volatile bool _isDisposed = false;
        private volatile bool _isConnecting = false;
        private int _nonceCounter = 0;

        private DiscordActivity _pendingActivity;
        private bool _hasPendingActivity = false;
        private string _pendingPongPayload = null;

        public DiscordIpcClient(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId))
                throw new ArgumentNullException("applicationId");

            ApplicationId = applicationId;
            LogInfo = delegate(string msg) { System.Console.WriteLine("[DiscordRPC-Info] " + msg); };
            LogWarn = delegate(string msg) { System.Console.WriteLine("[DiscordRPC-Warn] " + msg); };
            LogError = delegate(string msg) { System.Console.WriteLine("[DiscordRPC-Error] " + msg); };
        }

        private void InvokeLogInfo(string msg)
        {
            if (LogInfo != null)
                LogInfo(msg);
        }

        private void InvokeLogWarn(string msg)
        {
            if (LogWarn != null)
                LogWarn(msg);
        }

        private void InvokeLogError(string msg)
        {
            if (LogError != null)
                LogError(msg);
        }

        private bool HasBytesAvailable()
        {
            if (_stream == null || !IsConnected) return false;
            try
            {
                uint avail;
                if (PeekNamedPipe(_stream.SafePipeHandle.DangerousGetHandle(), null, 0, IntPtr.Zero, out avail, IntPtr.Zero))
                {
                    return avail >= 8;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Initiates the pipe connection on a background thread. Returns immediately without blocking.
        /// </summary>
        public void ConnectAsync()
        {
            if (IsConnected || _isConnecting || _isDisposed)
                return;

            _isConnecting = true;
            _workerThread = new Thread(WorkerLoop)
            {
                Name = "DiscordRPC-Worker",
                IsBackground = true
            };
            _workerThread.Start();
        }

        private void WorkerLoop()
        {
            while (!_isDisposed)
            {
                NamedPipeClientStream stream = null;
                int connectedPipeIndex = -1;

                // Search pipes discord-ipc-0 through discord-ipc-9 on this background thread
                for (int i = 0; i < 10; i++)
                {
                    if (_isDisposed) return;

                    string pipeName = "discord-ipc-" + i;
                    try
                    {
                        var candidate = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                        candidate.Connect(250); // Fast connection check per pipe
                        if (candidate.IsConnected)
                        {
                            stream = candidate;
                            connectedPipeIndex = i;
                            break;
                        }
                    }
                    catch
                    {
                        // Candidate pipe not available or busy
                    }
                }

                if (stream == null || !stream.IsConnected)
                {
                    InvokeLogWarn("Could not find any active Discord IPC pipe (discord-ipc-0 to 9). Retrying in 5 seconds...");
                    _isConnecting = false;

                    // Sleep in increments of 100ms so we can abort quickly on Dispose
                    for (int w = 0; w < 50; w++)
                    {
                        if (_isDisposed) return;
                        Thread.Sleep(100);
                    }
                    continue;
                }

                _stream = stream;
                IsConnected = true;
                _isConnecting = false;
                InvokeLogInfo(string.Format("Connected to Discord IPC pipe 'discord-ipc-{0}'", connectedPipeIndex));

                // Send Handshake on background thread
                try
                {
                    string handshakeJson = DiscordJson.CreateHandshake(ApplicationId);
                    DiscordIpcProtocol.WriteFrame(_stream, DiscordOpcode.Handshake, handshakeJson);
                    InvokeLogInfo(string.Format("Handshake sent for Client ID {0}. Awaiting READY...", ApplicationId));
                }
                catch (Exception ex)
                {
                    InvokeLogError("Failed to send handshake: " + ex.Message);
                    CleanupConnection();
                    continue;
                }

                // Process incoming frames and send queued activity updates on this single worker thread
                while (!_isDisposed && IsConnected && _stream != null)
                {
                    try
                    {
                        // 1. Check for incoming frames from Discord without blocking
                        while (HasBytesAvailable())
                        {
                            DiscordOpcode opcode;
                            string payload;
                            if (DiscordIpcProtocol.ReadFrame(_stream, out opcode, out payload))
                            {
                                HandleFrame(opcode, payload);
                            }
                            else
                            {
                                InvokeLogWarn("Failed to read frame from pipe (EOF or error).");
                                CleanupConnection();
                                break;
                            }
                        }

                        if (!IsConnected || _stream == null) break;

                        // 2. Handle pending Pong response if requested
                        string pongPayload = null;
                        lock (_activityLock)
                        {
                            if (_pendingPongPayload != null)
                            {
                                pongPayload = _pendingPongPayload;
                                _pendingPongPayload = null;
                            }
                        }

                        if (pongPayload != null)
                        {
                            try
                            {
                                DiscordIpcProtocol.WriteFrame(_stream, DiscordOpcode.Pong, pongPayload);
                                InvokeLogInfo("Pong dispatched.");
                            }
                            catch (Exception ex)
                            {
                                InvokeLogError("Failed to send Pong: " + ex.Message);
                            }
                        }

                        // 3. Handle pending Activity update
                        if (IsReady)
                        {
                            DiscordActivity toSend = null;
                            bool hasItem = false;
                            lock (_activityLock)
                            {
                                if (_hasPendingActivity)
                                {
                                    toSend = _pendingActivity;
                                    _hasPendingActivity = false;
                                    hasItem = true;
                                }
                            }

                            if (hasItem)
                            {
                                int pid = GetProcessId();
                                string nonce = (++_nonceCounter).ToString();
                                string json = DiscordJson.CreateSetActivity(pid, toSend, nonce);

                                try
                                {
                                    DiscordIpcProtocol.WriteFrame(_stream, DiscordOpcode.Frame, json);
                                    string details = (toSend != null && toSend.Details != null) ? toSend.Details : "(none)";
                                    string state = (toSend != null && toSend.State != null) ? toSend.State : "(none)";
                                    string largeImg = (toSend != null && toSend.LargeImageKey != null) ? toSend.LargeImageKey : "(none)";
                                    string smallImg = (toSend != null && toSend.SmallImageKey != null) ? toSend.SmallImageKey : "(none)";
                                    InvokeLogInfo(string.Format("Pipe frame dispatched (SET_ACTIVITY, nonce: {0}): Details=\"{1}\", State=\"{2}\", LargeImage=\"{3}\", SmallImage=\"{4}\"", nonce, details, state, largeImg, smallImg));
                                    InvokeLogInfo(string.Format("Dispatched activity payload (nonce {0}): {1}", nonce, json));
                                }
                                catch (Exception ex)
                                {
                                    InvokeLogError(string.Format("Failed to write activity frame to pipe: {0}", ex.Message));
                                }
                            }
                        }

                        // 4. Wait for send signal or poll incoming packets (50ms interval)
                        _sendEvent.WaitOne(50);
                    }
                    catch (Exception ex)
                    {
                        InvokeLogError(string.Format("Worker loop iteration exception: {0}", ex.ToString()));
                        break;
                    }
                }

                // Cleanup connection before attempting retry
                CleanupConnection();

                if (_isDisposed)
                    break;

                // Wait 3 seconds before attempting reconnect
                for (int w = 0; w < 30; w++)
                {
                    if (_isDisposed) return;
                    Thread.Sleep(100);
                }
            }
        }

        public void HandleFrame(DiscordOpcode opcode, string payload)
        {
            switch (opcode)
            {
                case DiscordOpcode.Frame:
                    ProcessDispatchFrame(payload);
                    break;

                case DiscordOpcode.Ping:
                    InvokeLogInfo("Ping received from Discord. Queueing Pong.");
                    lock (_activityLock)
                    {
                        _pendingPongPayload = payload;
                    }
                    _sendEvent.Set();
                    break;

                case DiscordOpcode.Pong:
                    InvokeLogInfo("Pong received from Discord.");
                    break;

                case DiscordOpcode.Close:
                    int? code = DiscordJson.ExtractInt(payload, "code");
                    string reason = DiscordJson.ExtractString(payload, "message");
                    if (reason == null) reason = "Unknown";
                    InvokeLogWarn(string.Format("Discord closed the connection: ({0}) {1}", code, reason));
                    IsReady = false;
                    if (OnClose != null)
                        OnClose(code.HasValue ? code.Value : 0, reason);
                    break;
            }
        }

        private void ProcessDispatchFrame(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            string cmd = DiscordJson.ExtractString(json, "cmd");
            string evt = DiscordJson.ExtractString(json, "evt");

            if (string.Equals(cmd, "DISPATCH", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(evt, "READY", StringComparison.OrdinalIgnoreCase))
                {
                    var user = new DiscordUser
                    {
                        Id = DiscordJson.ExtractNestedString(json, "user", "id"),
                        Username = DiscordJson.ExtractNestedString(json, "user", "username"),
                        Discriminator = DiscordJson.ExtractNestedString(json, "user", "discriminator"),
                        Avatar = DiscordJson.ExtractNestedString(json, "user", "avatar")
                    };

                    CurrentUser = user;
                    IsReady = true;
                    InvokeLogInfo(string.Format("Discord Ready! Logged in as: {0} (ID: {1})", user.Username, user.Id));
                    if (OnReady != null)
                        OnReady(user);
                }
                else if (string.Equals(evt, "ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    string msg = DiscordJson.ExtractString(json, "message");
                    if (msg == null) msg = "Unknown dispatch error";
                    InvokeLogError(string.Format("Discord dispatch error: {0}. Full response={1}", msg, json));
                    if (OnError != null)
                        OnError(msg);
                }
                else
                {
                    InvokeLogInfo(string.Format("Discord event dispatched ({0}): {1}", evt ?? "unknown", json));
                }
            }
            else if (string.Equals(cmd, "SET_ACTIVITY", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(evt, "ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    string msg = DiscordJson.ExtractString(json, "message");
                    int? errCode = DiscordJson.ExtractInt(json, "code");
                    InvokeLogError(string.Format("Discord SET_ACTIVITY error ({0}): {1}. Full response={2}", errCode.HasValue ? errCode.Value.ToString() : "none", msg ?? "Unknown", json));
                }
                else
                {
                    // Activity update ACK
                    InvokeLogInfo(string.Format("Discord acknowledged SET_ACTIVITY update. Response={0}", json));
                }
            }
            else
            {
                InvokeLogInfo(string.Format("Discord frame received (cmd: {0}, evt: {1}): {2}", cmd ?? "null", evt ?? "null", json));
            }
        }

        /// <summary>
        /// Non-blocking method to update the Discord activity. Enqueues the activity to be sent
        /// by the background worker thread and returns immediately.
        /// </summary>
        public bool SetActivity(DiscordActivity activity)
        {
            if (!IsReady || !IsConnected || _stream == null || _isDisposed)
            {
                return false;
            }

            lock (_activityLock)
            {
                _pendingActivity = activity;
                _hasPendingActivity = true;
            }
            _sendEvent.Set();
            return true;
        }

        public bool ClearActivity()
        {
            return SetActivity(null);
        }

        private void CleanupConnection()
        {
            IsConnected = false;
            IsReady = false;
            _isConnecting = false;

            if (_stream != null)
            {
                try { _stream.Close(); } catch { }
                try { _stream.Dispose(); } catch { }
                _stream = null;
            }

            try
            {
                if (OnDisconnected != null)
                    OnDisconnected();
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            InvokeLogInfo("Disposing Discord IPC client...");

            _sendEvent.Set(); // Wake up worker thread

            if (_stream != null && _stream.IsConnected)
            {
                try
                {
                    // Send clean Close frame
                    DiscordIpcProtocol.WriteFrame(_stream, DiscordOpcode.Close, "{\"code\":1000,\"message\":\"Normal Closure\"}");
                }
                catch { }
            }

            CleanupConnection();

            if (_workerThread != null && _workerThread.IsAlive)
            {
                try { _workerThread.Join(500); } catch { }
            }
        }
    }
}
