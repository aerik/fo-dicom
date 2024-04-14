// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using System;
    using System.Globalization;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// .NET implementation of <see cref="NetworkManager"/>.
    /// </summary>
    public class DesktopNetworkManager : NetworkManager
    {
        #region FIELDS

        /// <summary>
        /// Singleton instance of <see cref="DesktopNetworkManager"/>.
        /// </summary>
        public static readonly NetworkManager Instance;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes the static fields of <see cref="DesktopNetworkManager"/>.
        /// </summary>
        static DesktopNetworkManager()
        {
            Instance = new DesktopNetworkManager();
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Implementation of the machine name getter.
        /// </summary>
        protected override string MachineNameImpl
        {
            get
            {
#if NETSTANDARD
                return Environment.GetEnvironmentVariable("COMPUTERNAME");
#else
                return Environment.MachineName;
#endif
            }
        }

        #endregion

        #region METHODS

#if !NET35
        /// <summary>
        /// Platform-specific implementation to create a network listener object.
        /// </summary>
        /// <param name="port">Network port to listen to.</param>
        /// <returns>Network listener implementation.</returns>
        protected override INetworkListener CreateNetworkListenerImpl(int port)
        {
            return new DesktopNetworkListener(port);
        }

        /// <summary>
        /// Platform-specific implementation to create a network stream object.
        /// </summary>
        /// <param name="host">Network host.</param>
        /// <param name="port">Network port.</param>
        /// <param name="useTls">Use TLS layer?</param>
        /// <param name="noDelay">No delay?</param>
        /// <param name="ignoreSslPolicyErrors">Ignore SSL policy errors?</param>
        /// <returns>Network stream implementation.</returns>
        protected override INetworkStream CreateNetworkStreamImpl(string host, int port, bool useTls, bool noDelay, bool ignoreSslPolicyErrors, string certificateName)
        {
            return new DesktopNetworkStream(host, port, useTls, noDelay, ignoreSslPolicyErrors, certificateName);
        }

        /// <summary>
        /// Platform-specific implementation to check whether specified <paramref name="exception"/> represents a socket exception.
        /// </summary>
        /// <param name="exception">Exception to validate.</param>
        /// <param name="errorCode">Error code, valid if <paramref name="exception"/> is socket exception.</param>
        /// <param name="errorDescriptor">Error descriptor, valid if <paramref name="exception"/> is socket exception.</param>
        /// <returns>True if <paramref name="exception"/> is socket exception, false otherwise.</returns>
        protected override bool IsSocketExceptionImpl(Exception exception, out int errorCode, out string errorDescriptor)
        {
            var socketEx = exception as SocketException;
            if (socketEx != null)
            {
#if NETSTANDARD
                errorCode = (int)socketEx.SocketErrorCode;
#else
                errorCode = socketEx.ErrorCode;
#endif
                errorDescriptor = socketEx.SocketErrorCode.ToString();
                return true;
            }

            errorCode = -1;
            errorDescriptor = null;
            return false;
        }
#endif

        /// <summary>
        /// Get X509 certificate from the certificate store.
        /// </summary>
        /// <param name="certificateName">Certificate name.</param>
        /// <returns>Certificate with the specified name.</returns>
        protected override X509Certificate GetX509CertificateImpl(string certificateName)
        {
            try
            {
                X509Certificate2Collection certs = null;
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    certs = store.Certificates.Find(X509FindType.FindBySubjectName, certificateName, false);
                }

                if (certs.Count == 0)
                {
                    using (var store = new X509Store("WebHosting", StoreLocation.LocalMachine))
                    {
                        store.Open(OpenFlags.ReadOnly);
                        certs = store.Certificates.Find(X509FindType.FindBySubjectName, certificateName, false);
                    }
                }
                if (certs.Count == 0)
                {
                    throw new DicomNetworkException("Unable to find certificate for " + certificateName);
                }
                X509Certificate2 cert = certs[0];
                //check that we can access the private key, which we need to to to authenticate as server
                const String RSA = "1.2.840.113549.1.1.1";
                const String DSA = "1.2.840.10040.4.1";
                const String ECC = "1.2.840.10045.2.1";
                object privateKey = null;
                switch (cert.PublicKey.Oid.Value)
                {
                    case RSA:
                        privateKey = cert.GetRSAPrivateKey();
                        break;
                    case DSA:
                        privateKey = cert.GetDSAPrivateKey();
                        break;
                    case ECC:
                        privateKey = cert.GetECDsaPrivateKey();
                        break;
                }
                return cert;
            }
            catch (Exception x)
            {
                throw new DicomNetworkException("Cannot load private key for certificate: " + certificateName, x);
            }
        }

        /// <summary>
        /// Platform-specific implementation to attempt to obtain a unique network identifier, e.g. based on a MAC address.
        /// </summary>
        /// <param name="identifier">Unique network identifier, if found.</param>
        /// <returns>True if network identifier could be obtained, false otherwise.</returns>
        protected override bool TryGetNetworkIdentifierImpl(out DicomUID identifier)
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (NetworkInterface.LoopbackInterfaceIndex == i
                    || interfaces[i].OperationalStatus != OperationalStatus.Up) continue;

                var hex = interfaces[i].GetPhysicalAddress().ToString();
                if (string.IsNullOrEmpty(hex)) continue;

                try
                {
                    var mac = long.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    {
                        identifier = DicomUID.Append(DicomImplementation.ClassUID, mac);
                        return true;
                    }
                }
                catch
                {
                }
            }

            identifier = null;
            return false;
        }

        #endregion
    }
}
