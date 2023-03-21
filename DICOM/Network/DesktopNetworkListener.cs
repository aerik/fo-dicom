// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// .NET implementation of the <see cref="INetworkListener"/>.
    /// </summary>
    public class DesktopNetworkListener : INetworkListener
    {
        #region FIELDS

        private readonly TcpListener listener;

        private X509Certificate certificate = null;

        private readonly int _Port;

        private readonly object _locker = new object();

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes a new instance of the <see cref="DesktopNetworkListener"/> class. 
        /// </summary>
        /// <param name="port">
        /// TCP/IP port to listen to.
        /// </param>
        internal DesktopNetworkListener(int port)
        {
            _Port = port;
            this.listener = new TcpListener(IPAddress.Any, port);
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Start listening.
        /// </summary>
        /// <returns>An await:able <see cref="Task"/>.</returns>
        public Task StartAsync()
        {
            this.listener.Start();
            return Task.FromResult(0);
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Stop()
        {
            this.listener.Stop();
        }

        /// <summary>
        /// Wait until a network stream is trying to connect, and return the accepted stream.
        /// </summary>
        /// <param name="certificateName">Certificate name of authenticated connections.</param>
        /// <param name="noDelay">No delay?</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Connected network stream.</returns>
        public async Task<INetworkStream> AcceptNetworkStreamAsync(
            string certificateName,
            bool noDelay,
            CancellationToken token)
        {
            //just test the certificate when starting the listener
            if (!string.IsNullOrEmpty(certificateName) && this.certificate == null)
            {
                this.certificate = DesktopNetworkManager.GetX509Certificate(certificateName);
            }
            var awaiter =
                await
                Task.WhenAny(this.listener.AcceptTcpClientAsync(), Task.Delay(-1, token)).ConfigureAwait(false);

            var tcpClientTask = awaiter as Task<TcpClient>;
            if (tcpClientTask != null)
            {
                var tcpClient = tcpClientTask.Result;
                tcpClient.NoDelay = noDelay;

                if (!string.IsNullOrEmpty(certificateName) && this.certificate != null)
                {
                    //refresh the certifcate in case it's been updated while the service is running?
                    var cert = DesktopNetworkManager.GetX509Certificate(certificateName);
                    if (cert != null) this.certificate = cert;
                }

                return new DesktopNetworkStream(tcpClient, this.certificate);
            }
            return null;
        }

        #endregion
    }
}
