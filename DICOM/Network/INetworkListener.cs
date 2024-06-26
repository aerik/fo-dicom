﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for listening to network stream connections.
    /// </summary>
    public interface INetworkListener
    {
        /// <summary>
        /// Start listening.
        /// </summary>
        /// <returns>An await:able <see cref="Task"/>.</returns>
        Task StartAsync();

        /// <summary>
        /// Stop listening.
        /// </summary>
        void Stop();

        /// <summary>
        /// Wait until a network stream is trying to connect, and return the accepted stream.
        /// </summary>
        /// <param name="certificateName">Certificate name of authenticated connections.</param>
        /// <param name="noDelay">No delay?</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Connected network stream.</returns>
        Task<INetworkStream> AcceptNetworkStreamAsync(string certificateName, bool noDelay, CancellationToken token);

        /// <summary>
        /// Wait until a network stream is trying to connect, and return the accepted stream.
        /// </summary>
        /// <param name="certificateName">Certificate name of authenticated connections.</param>
        /// <param name="noDelay">No delay?</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Connected network stream.</returns>
        Task<TcpClient> AcceptTcpClientAsync(bool noDelay, CancellationToken token);

        Task<INetworkStream> GetNetorkStream(TcpClient tcpClient, string certificateName);

        Task<INetworkStream> GetNetorkStream(TcpClient tcpClient, X509Certificate certificate);
    }
}
