﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using Dicom.Log;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Representation of a DICOM server.
    /// </summary>
    /// <typeparam name="T">DICOM service that the server should manage.</typeparam>
    public class DicomServer<T> : IDicomServer
        where T : DicomService, IDicomServiceProvider
    {
        #region FIELDS

        private bool disposed;

        private readonly string certificateName;

        private readonly Encoding fallbackEncoding;

        private readonly CancellationTokenSource cancellationSource;

        private readonly List<T> clients;

        private readonly object _clientLocker = new object();

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes an instance of <see cref="DicomServer{T}"/>, that starts listening for connections in the background.
        /// </summary>
        /// <param name="port">Port to listen to.</param>
        /// <param name="certificateName">Certificate name for authenticated connections.</param>
        /// <param name="options">Service options.</param>
        /// <param name="fallbackEncoding">Fallback encoding.</param>
        /// <param name="logger">Logger, if null default logger will be applied.</param>
        [Obsolete("Use DicomServer.Create to instantiate DICOM server object")]
        public DicomServer(
            int port,
            string certificateName = null,
            DicomServiceOptions options = null,
            Encoding fallbackEncoding = null,
            Logger logger = null)
        {
            this.Port = port;
            this.certificateName = certificateName;
            this.fallbackEncoding = fallbackEncoding;
            this.cancellationSource = new CancellationTokenSource();
            this.clients = new List<T>();

            this.Options = options ?? DicomServiceOptions.Default;
            this.Logger = logger ?? LogManager.GetLogger("Dicom.Network");
            this.IsListening = false;
            this.Exception = null;

            this.BackgroundWorker = Task.WhenAll(OnTimerTickAsync(), ListenAsync());

            this.disposed = false;
            this.Register();
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the port to which the server is listening.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the logger used by <see cref="DicomServer{T}"/>
        /// </summary>
        public Logger Logger { get; }

        /// <summary>
        /// Gets the options to control behavior of <see cref="DicomService"/> base class.
        /// </summary>
        public DicomServiceOptions Options { get; }

        /// <summary>
        /// Gets a value indicating whether the server is actively listening for client connections.
        /// </summary>
        public bool IsListening { get; private set; }

        /// <summary>
        /// Gets the exception that was thrown if the server failed to listen.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Gets the <see cref="Task"/> managing the background listening and unused client removal processes.
        /// </summary>
        public Task BackgroundWorker { get; }

        #endregion

        #region METHODS

        /// <summary>
        /// Stop server from further listening.
        /// </summary>
        public void Stop()
        {
            if (!this.cancellationSource.IsCancellationRequested)
            {
                this.cancellationSource.Cancel();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Execute the disposal.
        /// </summary>
        /// <param name="disposing">True if called from <see cref="Dispose"/>, false otherwise.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Stop();
                this.cancellationSource.Dispose();
                lock (_clientLocker)
                {
                    this.clients.Clear();
                }
            }

            this.Unregister();
            this.disposed = true;
        }

        /// <summary>
        /// Register this server to list of registered servers.
        /// </summary>
        protected void Register()
        {
            var added = DicomServer.Add(this);
            if (!added)
            {
                this.Logger.Warn(
                    "Could not register DICOM server on port {0}, probably because another server is already registered on the same port.",
                    this.Port);
            }
        }

        /// <summary>
        /// Unregister this server from list of registered servers.
        /// </summary>
        protected void Unregister()
        {
            var removed = DicomServer.Remove(this);
            if (!removed)
            {
                this.Logger.Warn(
                    "Could not unregister DICOM server on port {0}, either because registration failed or because server has already been unregistered once.",
                    this.Port);
            }
        }

        /// <summary>
        /// Create an instance of the DICOM service class.
        /// </summary>
        /// <param name="stream">Network stream.</param>
        /// <returns>An instance of the DICOM service class.</returns>
        protected virtual T CreateScp(INetworkStream stream)
        {
            return (T)Activator.CreateInstance(typeof(T), stream, this.fallbackEncoding, this.Logger);
        }

        /// <summary>
        /// Listen indefinitely for network connections on the specified port.
        /// </summary>
        private async Task ListenAsync()
        {
            INetworkListener listener = null;
            try
            {
                var noDelay = this.Options?.TcpNoDelay ?? DicomServiceOptions.Default.TcpNoDelay;
                X509Certificate certificate = null;
                if (!String.IsNullOrWhiteSpace(this.certificateName))
                {
                    certificate = NetworkManager.GetX509Certificate(this.certificateName);
                }

                listener = NetworkManager.CreateNetworkListener(this.Port);
                await listener.StartAsync().ConfigureAwait(false);
                this.IsListening = true;

                while (!this.cancellationSource.IsCancellationRequested)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync(noDelay, this.cancellationSource.Token).ConfigureAwait(false);
                    if(tcpClient != null)
                    {
                        Task<INetworkStream> streamTask = listener.GetNetorkStream(tcpClient, certificate);
                        _ = streamTask.ContinueWith(task =>
                        {
                            try
                            {
                                if (!task.IsFaulted)
                                {
                                    var scp = this.CreateScp(task.Result);
                                    if (this.Options != null)
                                    {
                                        scp.Options = this.Options;
                                    }
                                    lock (_clientLocker)
                                    {
                                        this.clients.Add(scp);
                                    }
                                }
                            }
                            catch(DicomNetworkException dnx)
                            {
                                this.Logger.Error("Error getting network stream: " + dnx.Message);
                            }
                        });
                    }
                    //var networkStream =
                    //    await listener.AcceptNetworkStreamAsync(
                    //        this.certificateName,
                    //        noDelay,
                    //        this.cancellationSource.Token).ConfigureAwait(false);

                    //if (networkStream != null)
                    //{
                    //    var scp = this.CreateScp(networkStream);
                    //    if (this.Options != null)
                    //    {
                    //        scp.Options = this.Options;
                    //    }
                    //    lock (_clientLocker)
                    //    {
                    //        this.clients.Add(scp);
                    //    }
                    //}
                }

                listener.Stop();
                this.IsListening = false;
                this.Exception = null;
            }
            catch (OperationCanceledException)
            {
                this.Logger.Info("Listening manually terminated");
                this.Exception = null;
            }
            catch (Exception e)
            {
                this.Logger.Error("Exception listening for clients, {@error}", e);
                string debugMsg = "Exception listening for clients, " + e.Message;
                if(e.InnerException != null)
                {
                    debugMsg += " - " + e.InnerException.Message;
                }
                debugMsg += "\n\n" + e.StackTrace;
                this.Stop();
                this.Exception = e;
            }
            finally
            {
                if (listener != null)
                {
                    listener.Stop();
                }
                this.IsListening = false;
            }
        }

        /// <summary>
        /// Remove no longer used client connections.
        /// </summary>
        private async Task OnTimerTickAsync()
        {
            while (!this.cancellationSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, this.cancellationSource.Token).ConfigureAwait(false);
                    lock (_clientLocker)
                    {
                        for (int i = clients.Count - 1; i >= 0; i--)
                        {
                            var curClient = this.clients[i];
                            if (curClient == null)
                            {
                                this.clients.RemoveAt(i);
                            }
                            else
                            {
                                DateTime lastActiveUTC = curClient.LastActivityUTC;
                                var elapsed = DateTime.UtcNow - lastActiveUTC;
                                //wait one second to dispose everything
                                if (!curClient.IsConnected && elapsed.TotalSeconds > 1)
                                {
                                    curClient.Dispose();
                                    this.clients.RemoveAt(i);
                                }
                                else if (elapsed.TotalMinutes > 10) //close connections with no recent activity
                                {
                                    this.Logger.Warn("Client " + curClient.RemoteHost + " has no activity since " + lastActiveUTC.ToString() + " (UTC), disconnecting");
                                    curClient.Dispose();
                                    this.clients.RemoveAt(i);
                                }
                            }
                        }
                    }
                    //this.clients.RemoveAll(client => !client.IsConnected);
                }
                catch (OperationCanceledException)
                {
                    this.Logger.Info("Disconnected client cleanup manually terminated.");
                }
                catch (Exception e)
                {
                    this.Logger.Warn("Exception removing disconnected clients, {@error}", e);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Support class for managing multiple DICOM server instances.
    /// </summary>
    public static class DicomServer
    {
        #region FIELDS

        private static readonly HashSet<IDicomServer> Servers = new HashSet<IDicomServer>(DicomServerPortComparer.Default);

        private static readonly object locker = new object();

        #endregion

        #region METHODS

        /// <summary>
        /// Creates a DICOM server object.
        /// </summary>
        /// <typeparam name="T">DICOM service that the server should manage.</typeparam>
        /// <param name="port">Port to listen to.</param>
        /// <param name="certificateName">Certificate name for authenticated connections.</param>
        /// <param name="options">Service options.</param>
        /// <param name="fallbackEncoding">Fallback encoding.</param>
        /// <param name="logger">Logger, if null default logger will be applied.</param>
        /// <returns>An instance of <see cref="DicomServer{T}"/>, that starts listening for connections in the background.</returns>
        public static IDicomServer Create<T>(
            int port,
            string certificateName = null,
            DicomServiceOptions options = null,
            Encoding fallbackEncoding = null,
            Logger logger = null) where T : DicomService, IDicomServiceProvider
        {
            if (Servers.Any(server => server.Port == port))
            {
                throw new DicomNetworkException("There is already a DICOM server registered on port {0}", port);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            lock (locker)
            {
                return new DicomServer<T>(port, certificateName, options, fallbackEncoding, logger);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Gets DICOM server instance registered to <paramref name="port"/>.
        /// </summary>
        /// <param name="port">Port number for which DICOM server is requested.</param>
        /// <returns>Registered DICOM server for <paramref name="port"/>.</returns>
        public static IDicomServer GetInstance(int port)
        {
            return Servers.SingleOrDefault(server => server.Port == port);
        }

        /// <summary>
        /// Gets an indicator of whether a DICOM server is registered and listening on the specified <paramref name="port"/>.
        /// </summary>
        /// <param name="port">Port number for which listening status is requested.</param>
        /// <returns>True if DICOM server on <paramref name="port"/> is registered and listening, false otherwise.</returns>
        public static bool IsListening(int port)
        {
            return GetInstance(port)?.IsListening ?? false;
        }

        /// <summary>
        /// Adds a DICOM server to the list of registered servers.
        /// </summary>
        /// <param name="server">Server to add.</param>
        /// <returns>True if <paramref name="server"/> could be added, false otherwise.</returns>
        internal static bool Add(IDicomServer server)
        {
            lock (locker)
            {
                return Servers.Add(server);
            }
        }

        /// <summary>
        /// Removes a DICOM server from the list of registered servers.
        /// </summary>
        /// <param name="server">Server to remove.</param>
        /// <returns>True if <paramref name="server"/> could be removed, false otherwise.</returns>
        internal static bool Remove(IDicomServer server)
        {
            lock (locker)
            {
                return Servers.Remove(server);
            }
        }

        #endregion

        #region INNER TYPES

        /// <summary>
        /// Equality comparer implementation with respect to <see cref="IDicomServer"/> <see cref="IDicomServer.Port">port number</see>.
        /// </summary>
        private class DicomServerPortComparer : IEqualityComparer<IDicomServer>
        {
            public static readonly IEqualityComparer<IDicomServer> Default = new DicomServerPortComparer();

            private DicomServerPortComparer()
            {
            }

            public bool Equals(IDicomServer x, IDicomServer y)
            {
                return x != null && y != null && x.Port == y.Port;
            }

            public int GetHashCode(IDicomServer obj)
            {
                if (obj == null)
                {
                    throw new ArgumentNullException(nameof(obj));
                }
                return obj.Port;
            }
        }

        #endregion
    }
}
