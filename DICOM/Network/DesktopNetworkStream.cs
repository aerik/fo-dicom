// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// .NET implementation of <see cref="INetworkStream"/>.
    /// </summary>
    public sealed class DesktopNetworkStream : INetworkStream
    {
        #region FIELDS

        private bool disposed = false;

        private readonly TcpClient tcpClient;

        private readonly Stream networkStream;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes a client instance of <see cref="DesktopNetworkStream"/>.
        /// </summary>
        /// <param name="host">Network host.</param>
        /// <param name="port">Network port.</param>
        /// <param name="useTls">Use TLS layer?</param>
        /// <param name="noDelay">No delay?</param>
        /// <param name="ignoreSslPolicyErrors">Ignore SSL policy errors?</param>
        internal DesktopNetworkStream(string host, int port, bool useTls, bool noDelay, bool ignoreSslPolicyErrors, string certificateName)
        {
            this.RemoteHost = host;
            this.RemotePort = port;

#if NETSTANDARD
            this.tcpClient = new TcpClient { NoDelay = noDelay };
            this.tcpClient.ConnectAsync(host, port).Wait();
#else
            this.tcpClient = new TcpClient(host, port) { NoDelay = noDelay };
#endif

            Stream stream = this.tcpClient.GetStream();
            if (useTls)
            {
                X509CertificateCollection certs = null;
                if (!String.IsNullOrEmpty(certificateName))
                {
                    var cert = DesktopNetworkManager.GetX509Certificate(certificateName);
                    if (cert != null) certs = new X509CertificateCollection(new X509Certificate[] { cert });
                }
                var ssl = new SslStream(
                    stream,
                    false,
                    new RemoteCertificateValidationCallback(VerifyCertificate), null, EncryptionPolicy.RequireEncryption);
#if !DEBUG
                ssl.ReadTimeout = 5000;
                ssl.WriteTimeout = 5000;
#endif

#if NETSTANDARD
                ssl.AuthenticateAsClientAsync(host).Wait();
#else
                //ssl.AuthenticateAsClientAsync(host, certs, SslProtocols.Tls11 | SslProtocols.Tls12, false).Wait(5000);
                try
                {
                    ssl.AuthenticateAsClient(host, certs, SslProtocols.Tls11 | SslProtocols.Tls12, false);
                }
                catch (Exception x)
                {
                    string err = x.Message;
                    throw new DicomNetworkException("Could not authenticate SSL connection as client: " + x.Message);
                }
#endif
                stream = ssl;
                this.Authenticated = ssl.IsMutuallyAuthenticated;
                this.Encrypted = ssl.IsEncrypted; ;
                stream.ReadTimeout = -1;
                stream.WriteTimeout = -1;
            }
            //possibly reset
            stream.ReadTimeout = -1;
            stream.WriteTimeout = -1;
            this.LocalHost = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Address.ToString();
            this.LocalPort = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Port;

            this.networkStream = stream;
        }

        /// <summary>
        /// Initializes a server instance of <see cref="DesktopNetworkStream"/>.
        /// </summary>
        /// <param name="tcpClient">TCP client.</param>
        /// <param name="certificate">Certificate for authenticated connection.</param>
        /// <remarks>Ownership of <paramref name="tcpClient"/> remains with the caller, including responsibility for
        /// disposal. Therefore, a handle to <paramref name="tcpClient"/> is <em>not</em> stored when <see cref="DesktopNetworkStream"/>
        /// is initialized with this server-side constructor.</remarks>
        internal DesktopNetworkStream(TcpClient tcpClient, X509Certificate certificate)
        {
            this.LocalHost = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Address.ToString();
            this.LocalPort = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Port;
            this.RemoteHost = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            this.RemotePort = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;

            Stream stream = tcpClient.GetStream();
            if (certificate != null)
            {
                var certSelection = new LocalCertificateSelectionCallback((object sender, string targetHost,
                    X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers) =>
                {
                    return certificate;
                });

                var ssl = new SslStream(stream, false, new RemoteCertificateValidationCallback(DummyCertificatValidationCallback),
                    certSelection, EncryptionPolicy.RequireEncryption);
#if !DEBUG
                ssl.ReadTimeout = 5000;
                ssl.WriteTimeout = 5000;
#endif
#if NETSTANDARD
                ssl.AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls, false).Wait();
#else
                //clientCertificateRequired is only a request, not an actual requirement
                //https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream.authenticateasserver?view=netframework-4.7.2
                //ssl.AuthenticateAsServerAsync(certificate, true, SslProtocols.Tls11 | SslProtocols.Tls12, false).Wait(5000);
                try
                {
                    ssl.AuthenticateAsServer(certificate, true, SslProtocols.Tls11 | SslProtocols.Tls12, false);
                }
                catch (Exception x)
                {
                    try
                    {
                        ssl.Dispose();
                    }
                    catch { }
                    throw new DicomNetworkException("Could not authenticate SSL connection as server",x);
                }
#endif
                stream = ssl;
                this.Authenticated = ssl.IsMutuallyAuthenticated;
                this.Encrypted = ssl.IsEncrypted;
                stream.ReadTimeout = -1;
                stream.WriteTimeout = -1;
                string msg = "Secure connection established; Encrypted: " + ssl.IsEncrypted.ToString() + ", Mutually Authenticated: " + ssl.IsMutuallyAuthenticated.ToString();
                msg += ", SslProtocol: " + ssl.SslProtocol.ToString();
                Log.LogManager.GetLogger("DicomServer").Info(msg);
            }
            this.networkStream = stream;
        }

        private static bool DummyCertificatValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOkay = false;
            string logMsg = "Connection requested ";
            if (certificate != null)
            {
                logMsg += "with certficate '" + certificate.Subject + "'";
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    isOkay = true;
                }
                else
                {
                    //check for local certs
                    if (CertIsStoredLocally(certificate))
                    {
                        isOkay = true;
                        logMsg += ", matches local";
                    }
                    else
                    {
                        logMsg += ", but has errors: " + sslPolicyErrors.ToString();
                    }
                }
                logMsg += "\nCertificate:\n" + certificate.ToString().Replace("\r", "").Replace("]\n", "]").Replace("\n\n", "\n");
            }
            else
            {
                logMsg += "with anonymous TLS encryption";
                sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNotAvailable;
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    isOkay = true;
                }
                else
                {
                    logMsg += ", but has errors: " + sslPolicyErrors.ToString();
                }
            }
            if (isOkay)
            {
                Log.LogManager.GetLogger("DicomServer").Info(logMsg);
                return true;
            }
            Log.LogManager.GetLogger("DicomServer").Warn(logMsg);
            return false;
        }
        //private static X509Certificate LocalCertificateValidationCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        //{
        //    if (localCertificates != null && localCertificates.Count > 0) return localCertificates[0];
        //    return null;
        //}
        static bool VerifyCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null)
            {
                Log.LogManager.GetLogger("DicomClient").Warn("TLS connection with no certificate");
                return false;
            }
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                Log.LogManager.GetLogger("DicomClient").Info("TLS connection with certificate:" + certificate.Subject);
                return true;
            }
            try
            {
                //check for local certs
                if (CertIsStoredLocally(certificate)) return true;
            }
            catch (Exception ex)
            {
                Log.LogManager.GetLogger("DicomClient").Warn("TLS certificate:" + certificate.Subject + " could not be located: " + ex.Message);
            }
            Log.LogManager.GetLogger("DicomClient").Warn("TLS connection with certificate:" + certificate.Subject + " has errors " + sslPolicyErrors.ToString());
            return false;
        }

        private static bool CertIsStoredLocally(X509Certificate certificate)
        {
            if (certificate != null)
            {
                X509Certificate2Collection certs = null;
                string certSerial = certificate.GetSerialNumberString();
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    certs = store.Certificates.Find(X509FindType.FindBySerialNumber, certSerial, false);
                }
                if (certs == null || certs.Count == 0)
                {
                    using (var store = new X509Store("WebHosting", StoreLocation.LocalMachine))
                    {
                        store.Open(OpenFlags.ReadOnly);
                        certs = store.Certificates.Find(X509FindType.FindBySerialNumber, certSerial, false);
                    }
                }
                if (certs != null && certs.Count == 1 && certs[0].GetCertHashString() == certificate.GetCertHashString())
                {
                    //todo: check cert validity?
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DesktopNetworkStream()
        {
            this.Dispose(false);
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the remote host of the network stream.
        /// </summary>
        public string RemoteHost { get; }

        /// <summary>
        /// Gets the local host of the network stream.
        /// </summary>
        public string LocalHost { get; }

        /// <summary>
        /// Gets the remote port of the network stream.
        /// </summary>
        public int RemotePort { get; }

        /// <summary>
        /// Gets the local port of the network stream.
        /// </summary>
        public int LocalPort { get; }

        public bool Encrypted { get; }

        public bool Authenticated { get; }

        #endregion

        #region METHODS

        public Socket GetSocket()
        {
            if (this.tcpClient != null)
            {
                return this.tcpClient.Client;
            }
            return null;
        }

        public bool IsConnected()
        {
            Socket sock = GetSocket();
            if (sock != null) return sock.Connected;
            return false;
        }

        /// <summary>
        /// Get corresponding <see cref="Stream"/> object.
        /// </summary>
        /// <returns>Network stream as <see cref="Stream"/> object.</returns>
        public Stream AsStream()
        {
            return this.networkStream;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Do the actual disposal.
        /// </summary>
        /// <param name="disposing">True if called from <see cref="Dispose"/>, false otherwise.</param>
        /// <remarks>The underlying stream is normally passed on to a <see cref="DicomService"/> implementation that
        /// is responsible for disposing the stream when appropriate. Therefore, the stream should not be disposed here.</remarks>
        private void Dispose(bool disposing)
        {
            if (this.disposed) return;

            if (this.tcpClient != null)
            {
#if NETSTANDARD
                this.tcpClient.Dispose();
#else
                this.tcpClient.Close();
#endif
            }

            this.disposed = true;
        }

        #endregion
    }
}
