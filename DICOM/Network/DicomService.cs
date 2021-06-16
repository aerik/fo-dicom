// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Dicom.Imaging.Codec;
    using Dicom.IO;
    using Dicom.IO.Reader;
    using Dicom.IO.Writer;
    using Dicom.Log;

    /// <summary>
    /// Base class for DICOM network services.
    /// </summary>
    public abstract class DicomService : IDisposable
    {
        public enum DicomAssociationState {None=0, Requested=1, Accepted=2, Rejected=3, ReleaseRequested=4, Released=5 }

        #region FIELDS

        private bool _disposed = false;

        protected Stream _dimseStream;

        protected IFileReference _dimseStreamFile;

        private readonly INetworkStream _network;

        private readonly object _lock;

        private volatile bool _writing;

        private volatile bool _sending;

        private readonly Queue<PDU> _pduQueue;

        private readonly Queue<DicomMessage> _msgQueue;

        private readonly List<DicomRequest> _pending;

        private DicomMessage _dimse;

        private int _readLength;

        private readonly Encoding _fallbackEncoding;

        private readonly ManualResetEventSlim _pduQueueWatcher;

        private readonly Task _pduListener;

        private readonly object _SpeedLocker = new object();
        private DateTime _SpeedCheckTime = DateTime.Now;

        private long _BytesSentCounter = 0;
        private double _NetworkSentMs = 0;
        private long _BytesReceivedCounter = 0;
        private double _NetworkReceivedMs = 0;

        private readonly string _RemoteHost;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes a new instance of the <see cref="DicomService"/> class.
        /// </summary>
        /// <param name="stream">Network stream.</param>
        /// <param name="fallbackEncoding">Fallback encoding.</param>
        /// <param name="log">Logger</param>
        protected DicomService(INetworkStream stream, Encoding fallbackEncoding, Logger log)
        {
            _network = stream;
            _RemoteHost = _network.RemoteHost;
            _lock = new object();
            _pduQueue = new Queue<PDU>();
            _pduQueueWatcher = new ManualResetEventSlim(true);
            MaximumPDUsInQueue = 16;
            _msgQueue = new Queue<DicomMessage>();
            _pending = new List<DicomRequest>();
            IsConnected = true;
            _fallbackEncoding = fallbackEncoding ?? DicomEncoding.Default;
            Logger = log ?? LogManager.GetLogger("Dicom.Network");
            Options = new DicomServiceOptions();

            _pduListener = ListenAndProcessPDUAsync();
        }

        #endregion

        #region PROPERTIES

        public DateTime LastActivity
        {
            get
            {
                lock (_SpeedLocker)
                {
                    return _SpeedCheckTime;
                }
            }
        }

        public string RemoteHost
        {
            get
            {
                return _RemoteHost;
            }
        }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        public Logger Logger { get; set; }

        /// <summary>
        /// Gets or sets the DICOM service options.
        /// </summary>
        public DicomServiceOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the log ID.
        /// </summary>
        public string LogID { get; private set; }

        /// <summary>
        /// Gets or sets a user state associated with the service.
        /// </summary>
        public object UserState { get; set; }

        /// <summary>
        /// Gets the DICOM association.
        /// </summary>
        public DicomAssociation Association { get; internal set; }

        public DicomAssociationState AssociationState { get; private set; }

        /// <summary>
        /// Gets whether or not the service is connected.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets whether or not the send queue is empty.
        /// </summary>
        public bool IsSendQueueEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _msgQueue.Count == 0 && _pending.Count == 0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of PDUs in queue.
        /// </summary>
        public int MaximumPDUsInQueue { get; set; }

        #endregion

        #region METHODS

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            IsConnected = false;
            if (disposing)
            {
                _dimseStream?.Dispose();
                _network?.Dispose();//this might not be "owned" by the DicomService... TBD
                _pduQueueWatcher?.Dispose();
                string logMsg = "";
                if (LogID != null) logMsg = LogID;
                if (this is IDicomServiceUser)
                {
                    if (this.Association != null)
                    {
                        logMsg += " -> ";
                    }
                    logMsg += "DicomSCU";
                }
                else
                {
                    if (this.Association != null)
                    {
                        logMsg += " -> ";
                    }
                    logMsg += "DicomSCP";
                }
                logMsg += " connection disposed, Association state: " + AssociationState.ToString();
                if(AssociationState == DicomAssociationState.Released)
                {
                    Logger.Info(logMsg);
                }
                else
                {
                    Logger.Warn(logMsg);
                }

            }
            _disposed = true;
        }

        private void CalcSpeed(long byteCount, double networkMs, bool dirIsUp, bool force = false)
        {
            string logMsg = null;
            lock (_SpeedLocker)
            {
                if (dirIsUp)
                {
                    _BytesSentCounter += byteCount;
                    _NetworkSentMs += networkMs;
                }
                else
                {
                    _BytesReceivedCounter += byteCount;
                    _NetworkReceivedMs += networkMs;
                }
                TimeSpan ts = DateTime.Now - _SpeedCheckTime;
                double totalMs = ts.TotalMilliseconds;
                long totalBytes = _BytesReceivedCounter + _BytesSentCounter;
                if((force && totalMs>0) || totalMs > 5000)
                {
                    double bytesSentPerMs = _BytesSentCounter / _NetworkSentMs;
                    double bytesRecdPerMs = _BytesReceivedCounter / _NetworkReceivedMs;
                    double bytesTotalPerMs = totalBytes / totalMs;//includes non-network time
                    uint upMbps = (uint)Math.Round(bytesSentPerMs * 8 / 1000);
                    uint downMbps = (uint)Math.Round(bytesRecdPerMs * 8 / 1000);
                    uint totalMbps = (uint)Math.Round(bytesTotalPerMs * 8 / 1000);
                    logMsg = "Speed: " + totalMbps + "Mbps total , " + upMbps + "Mbps up (" + _BytesSentCounter+" bytes), " + downMbps + "Mbps down (" + _BytesReceivedCounter+" bytes)";
                    _BytesSentCounter = 0;
                    _BytesReceivedCounter = 0;
                    _NetworkSentMs = 0;
                    _NetworkReceivedMs = 0;
                    _SpeedCheckTime = DateTime.Now;
                }
            }
            if (logMsg != null && this.Association != null)
            {
                Logger.Info("{logAE} <-> "+logMsg, LogID);
            }
        }


        /// <summary>
        /// Send request from service.
        /// </summary>
        /// <param name="request">Request to send.</param>
        public virtual void SendRequest(DicomRequest request)
        {
            SendMessage(request);
        }

        /// <summary>
        /// Send response from service.
        /// </summary>
        /// <param name="response">Response to send.</param>
        protected void SendResponse(DicomResponse response)
        {
            SendMessage(response);
        }

        /// <summary>
        /// The purpose of this method is to return the Stream that a SopInstance received
        /// via CStoreSCP will be written to.  This default implementation creates a temporary
        /// file and returns a FileStream on top of it.  Child classes can override this to write
        /// to another stream and avoid the I/O associated with the temporary file if so desired.
        /// Beware that some SopInstances can be very large so using a MemoryStream() could cause
        /// out of memory situations.
        /// </summary>
        /// <param name="file">A DicomFile with FileMetaInfo populated.</param>
        /// <returns>The stream to write the SopInstance to.</returns>
        protected virtual void CreateCStoreReceiveStream(DicomFile file)
        {
            _dimseStreamFile = TemporaryFile.Create();

            _dimseStream = _dimseStreamFile.Open();
            file.Save(_dimseStream);
            _dimseStream.Seek(0, SeekOrigin.End);
        }

        /// <summary>
        /// The purpose of this method is to create a DicomFile for the SopInstance received via
        /// CStoreSCP to pass to the IDicomCStoreProvider.OnCStoreRequest method for processing.
        /// This default implementation will return a DicomFile if the stream created by
        /// CreateCStoreReceiveStream() is seekable or null if it is not.  Child classes that 
        /// override CreateCStoreReceiveStream may also want override this to return a DicomFile 
        /// for unseekable streams or to do cleanup related to receiving that specific instance.  
        /// </summary>
        /// <returns>The DicomFile or null if the stream is not seekable.</returns>
        protected virtual DicomFile GetCStoreDicomFile()
        {
            if (_dimseStreamFile != null)
            {
                if (_dimseStream != null) _dimseStream.Dispose();
                return DicomFile.Open(_dimseStreamFile, _fallbackEncoding);
            }

            if (_dimseStream != null && _dimseStream.CanSeek)
            {
                _dimseStream.Seek(0, SeekOrigin.Begin);
                return DicomFile.Open(_dimseStream, _fallbackEncoding);
            }

            return null;
        }

        /// <summary>
        /// Asynchronously send single PDU.
        /// </summary>
        /// <param name="pdu">PDU to send.</param>
        /// <returns>Awaitable task.</returns>
        protected async Task SendPDUAsync(PDU pdu)
        {
            if (!IsConnected) return;
            //if (_disposed) return;
            _pduQueueWatcher.Wait();

            lock (_lock)
            {
                _pduQueue.Enqueue(pdu);
                if (_pduQueue.Count >= MaximumPDUsInQueue) _pduQueueWatcher.Reset();
            }

            await SendNextPDUAsync().ConfigureAwait(false);
        }

        private async Task SendNextPDUAsync()
        {
            while (true)
            {
                if (!IsConnected) return;

                PDU pdu;

                lock (_lock)
                {
                    if (_writing) return;

                    if (_pduQueue.Count == 0) return;

                    _writing = true;

                    pdu = _pduQueue.Dequeue();
                    if (_pduQueue.Count < MaximumPDUsInQueue) _pduQueueWatcher.Set();
                }

                if (Options.LogDataPDUs && pdu is PDataTF) Logger.Info("{logId} -> {pdu}", LogID, pdu);

                MemoryStream ms = new MemoryStream();
                pdu.Write().WritePDU(ms);

                byte[] buffer = ms.ToArray();

                try
                {
                    DateTime start = DateTime.Now;
                    await _network.AsStream().WriteAsync(buffer, 0, (int)ms.Length).ConfigureAwait(false);
                    TimeSpan elapsed = DateTime.Now - start;
                    CalcSpeed(ms.Length,elapsed.TotalMilliseconds,true);
                }
                catch (ObjectDisposedException)
                {
                    // silently ignore
                    Logger.Warn("Object disposed while writing");
                    TryCloseConnection(force: true);
                }
                catch (NullReferenceException)
                {
                    // connection already closed; silently ignore
                    Logger.Warn("Null reference while writing");
                    TryCloseConnection(force: true);
                }
                catch (IOException e)
                {
                    LogIOException(e, Logger, false);
                    TryCloseConnection(e, true);
                }
                catch (AggregateException agx)
                {
                    if (agx.InnerException != null && agx.InnerException is IOException)
                    {
                        LogIOException(agx.InnerException as IOException, Logger, true);
                        TryCloseConnection(agx.InnerException, true);
                    }
                    else
                    {
                        Logger.Error("AggregateException sending PDU: " + agx.ToString());
                        TryCloseConnection(agx);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Exception sending PDU: {@error}", e);
                    TryCloseConnection(e);
                }

                lock (_lock) _writing = false;
            }
        }

        private async Task<int> ReadStreamAsync(Stream stream, byte[] buffer, int offset, int count, int timeoutMs)
        {
            //Task<int> readTask = Task<int>.Factory.StartNew(() => {
            //    return stream.Read(buffer, offset, count);
            //})

            if (stream.CanRead)
            {
                DateTime start = DateTime.Now;
                Task<int> readTask = stream.ReadAsync(buffer, offset, count);
                Task delayTask = Task.Delay(timeoutMs);
                Task task = await Task.WhenAny(readTask, delayTask);

                if (task == readTask)
                {
                    int numBytes = readTask.Result;
                    TimeSpan elapsed = DateTime.Now - start;
                    CalcSpeed(numBytes, elapsed.TotalMilliseconds, false);
                    return numBytes;
                }
            }
            return 0;
        }

        private int ReadStream(Stream stream, byte[] buffer, int offset, int count)
        {
            DateTime start = DateTime.Now;
            int numBytes = stream.Read(buffer, offset, count);
            TimeSpan elapsed = DateTime.Now - start;
            CalcSpeed(numBytes, elapsed.TotalMilliseconds, false);
            return numBytes;
        }
        private Task ListenAndProcessPDUAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    while (IsConnected)
                    {
                        var stream = _network.AsStream();

                        // Read PDU header
                        _readLength = 6;

                        var buffer = new byte[6];
                        var count = 0;
                        if (stream.CanRead)
                        {
                            count = ReadStream(stream, buffer, 0, 6);
                            //count = await ReadStreamAsync(stream, buffer, 0, 6, readTimeoutMs).ConfigureAwait(false);
                            //count = await stream.ReadAsync(buffer, 0, 6).ConfigureAwait(false);
                        }
                        do
                        {
                            if (count == 0)
                            {
                                if (IsConnected)
                                {
                                    string logMsg = "";
                                    if (LogID != null) logMsg = LogID;
                                    Logger.Info(logMsg + " No Bytes read - disconnecting");
                                    TryCloseConnection();
                                }
                                return;
                            }

                            _readLength -= count;
                            if (_readLength > 0)
                            {
                                count = ReadStream(stream, buffer, 6 - _readLength, _readLength);
                                //count = await ReadStreamAsync(stream, buffer, 6 - _readLength, _readLength, readTimeoutMs).ConfigureAwait(false);
                                //count = await stream.ReadAsync(buffer, 6 - _readLength, _readLength).ConfigureAwait(false);
                            }
                        }
                        while (_readLength > 0);

                        var length = BitConverter.ToInt32(buffer, 2);
                        length = Endian.Swap(length);

                        _readLength = length;

                        Array.Resize(ref buffer, length + 6);
                        count = ReadStream(stream, buffer, 6, length);
                        //count = await ReadStreamAsync(stream, buffer, 6, length, readTimeoutMs).ConfigureAwait(false);
                        //count = await stream.ReadAsync(buffer, 6, length).ConfigureAwait(false);

                        // Read PDU
                        do
                        {
                            if (count == 0)
                            {
                                // disconnected
                                TryCloseConnection();
                                return;
                            }

                            _readLength -= count;
                            if (_readLength > 0)
                            {
                                count = ReadStream(stream, buffer, buffer.Length - _readLength, _readLength);
                                //count = await stream.ReadAsync(buffer, buffer.Length - _readLength, _readLength).ConfigureAwait(false);
                            }
                        }
                        while (_readLength > 0);

                        var raw = new RawPDU(buffer);

                        switch (raw.Type)
                        {
                            case 0x01:
                                {
                                    Association = new DicomAssociation
                                    {
                                        RemoteHost = _network.RemoteHost,
                                        RemotePort = _network.RemotePort,
                                        LocalHost = _network.LocalHost,
                                        LocalPort = _network.LocalPort
                                    };
                                    var pdu = new AAssociateRQ(Association);
                                    pdu.Read(raw);
                                    LogID = Association.CallingAE + " (" + Association.AssociationId.ToString() + ")";
                                    if (Options.UseRemoteAEForLogName)
                                    {
                                        Logger = LogManager.GetLogger(LogID);
                                    }
                                    else
                                    {
                                        Logger = LogManager.GetLogger(Association.CalledAE);
                                    }
                                    Logger.Info(
                                        "{callingAE} <- Association request:\n{association}",
                                        LogID,
                                        Association.ToString());
                                    AssociationState = DicomAssociationState.Requested;
                                    (this as IDicomServiceProvider)?.OnReceiveAssociationRequest(Association);
                                    break;
                                }
                            case 0x02:
                                {
                                    var pdu = new AAssociateAC(Association);
                                    pdu.Read(raw);
                                    LogID = Association.CalledAE + " (" + Association.AssociationId.ToString() + ")";
                                    Logger.Info(
                                        "{calledAE} <- Association accept:\n{assocation}",
                                        LogID,
                                        Association.ToString());
                                    AssociationState = DicomAssociationState.Accepted;
                                    (this as IDicomServiceUser)?.OnReceiveAssociationAccept(Association);
                                    break;
                                }
                            case 0x03:
                                {
                                    var pdu = new AAssociateRJ();
                                    pdu.Read(raw);
                                    Logger.Info(
                                        "{logId} <- Association reject [result: {pduResult}; source: {pduSource}; reason: {pduReason}]",
                                        LogID,
                                        pdu.Result,
                                        pdu.Source,
                                        pdu.Reason);
                                    AssociationState = DicomAssociationState.Rejected;
                                    (this as IDicomServiceUser)?.OnReceiveAssociationReject(
                                        pdu.Result,
                                        pdu.Source,
                                        pdu.Reason);
                                    if (TryCloseConnection()) return;
                                    break;
                                }
                            case 0x04:
                                {
                                    var pdu = new PDataTF();
                                    pdu.Read(raw);
                                    if (Options.LogDataPDUs) Logger.Info("{logId} <- {@pdu}", LogID, pdu);
                                    //await this.ProcessPDataTFAsync(pdu).ConfigureAwait(false);
                                    this.ProcessPDataTFAsync(pdu).Wait();
                                    break;
                                }
                            case 0x05:
                                {
                                    var pdu = new AReleaseRQ();
                                    pdu.Read(raw);
                                    Logger.Info("{logId} <- Association release request", LogID);
                                    AssociationState = DicomAssociationState.ReleaseRequested;
                                    (this as IDicomServiceProvider)?.OnReceiveAssociationReleaseRequest();
                                    break;
                                }
                            case 0x06:
                                {
                                    var pdu = new AReleaseRP();
                                    pdu.Read(raw);
                                    Logger.Info("{logId} <- Association release response", LogID);
                                    AssociationState = DicomAssociationState.Released;
                                    (this as IDicomServiceUser)?.OnReceiveAssociationReleaseResponse();
                                    if (TryCloseConnection()) return;
                                    break;
                                }
                            case 0x07:
                                {
                                    var pdu = new AAbort();
                                    pdu.Read(raw);
                                    Logger.Info(
                                        "{logId} <- Abort: {pduSource} - {pduReason}",
                                        LogID,
                                        pdu.Source,
                                        pdu.Reason);
                                    (this as IDicomService)?.OnReceiveAbort(pdu.Source, pdu.Reason);
                                    if (TryCloseConnection()) return;
                                    break;
                                }
                            case 0xFF:
                                {
                                    break;
                                }
                            default:
                                throw new DicomNetworkException("Unknown PDU type");
                        }
                    }
                }
                catch (ObjectDisposedException ode)
                {
                    TryCloseConnection(ode, true);
                }
                catch (NullReferenceException nre)
                {
                    TryCloseConnection(nre, true);
                }
                catch (IOException e)
                {
                    // LogIOException returns true for underlying socket error (probably due to forcibly closed connection), 
                    LogIOException(e, Logger, true);
                    TryCloseConnection(e, true);
                }
                catch (AggregateException agx)
                {
                    if (agx.InnerException != null && agx.InnerException is IOException)
                    {
                        LogIOException(agx.InnerException as IOException, Logger, true);
                        TryCloseConnection(agx.InnerException, true);
                    }
                    else
                    {
                        Logger.Error("AggregateException processing PDU: " + agx.ToString());
                        TryCloseConnection(agx);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Exception processing PDU: " + e.ToString());
                    TryCloseConnection(e);
                }
                Logger.Debug("Ending ListenAndProcessPDUAsync");
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Process P-DATA-TF PDUs.
        /// </summary>
        /// <param name="pdu">PDU to process.</param>
        private async Task ProcessPDataTFAsync(PDataTF pdu)
        {
            try
            {
                foreach (var pdv in pdu.PDVs)
                {
                    if (_dimse == null)
                    {
                        // create stream for receiving command
                        if (_dimseStream == null)
                        {
                            _dimseStream = new MemoryStream();
                            _dimseStreamFile = null;
                        }
                    }
                    else
                    {
                        // create stream for receiving dataset
                        if (_dimseStream == null)
                        {
                            if (_dimse.Type == DicomCommandField.CStoreRequest)
                            {
                                var pc = Association.PresentationContexts.FirstOrDefault(x => x.ID == pdv.PCID);

                                var file = new DicomFile();
                                file.FileMetaInfo.MediaStorageSOPClassUID = pc.AbstractSyntax;
                                file.FileMetaInfo.MediaStorageSOPInstanceUID =
                                    _dimse.Command.Get<DicomUID>(DicomTag.AffectedSOPInstanceUID);
                                file.FileMetaInfo.TransferSyntax = pc.AcceptedTransferSyntax;
                                file.FileMetaInfo.ImplementationClassUID = Association.RemoteImplementationClassUID;
                                file.FileMetaInfo.ImplementationVersionName = Association.RemoteImplementationVersion;
                                file.FileMetaInfo.SourceApplicationEntityTitle = Association.CallingAE;

                                CreateCStoreReceiveStream(file);
                            }
                            else
                            {
                                _dimseStream = new MemoryStream();
                                _dimseStreamFile = null;
                            }
                        }
                    }

                    await _dimseStream.WriteAsync(pdv.Value, 0, pdv.Value.Length).ConfigureAwait(false);

                    if (pdv.IsLastFragment)
                    {
                        if (pdv.IsCommand)
                        {
                            _dimseStream.Seek(0, SeekOrigin.Begin);

                            var command = new DicomDataset();

                            var reader = new DicomReader();
                            reader.IsExplicitVR = false;
                            reader.Read(new StreamByteSource(_dimseStream), new DicomDatasetReaderObserver(command));

                            _dimseStream = null;
                            _dimseStreamFile = null;

                            var type = command.Get<DicomCommandField>(DicomTag.CommandField);
                            switch (type)
                            {
                                case DicomCommandField.CStoreRequest:
                                    _dimse = new DicomCStoreRequest(command);
                                    break;
                                case DicomCommandField.CStoreResponse:
                                    _dimse = new DicomCStoreResponse(command);
                                    break;
                                case DicomCommandField.CFindRequest:
                                    _dimse = new DicomCFindRequest(command);
                                    break;
                                case DicomCommandField.CFindResponse:
                                    _dimse = new DicomCFindResponse(command);
                                    break;
                                case DicomCommandField.CGetRequest:
                                    _dimse = new DicomCGetRequest(command);
                                    break;
                                case DicomCommandField.CGetResponse:
                                    _dimse = new DicomCGetResponse(command);
                                    break;
                                case DicomCommandField.CMoveRequest:
                                    _dimse = new DicomCMoveRequest(command);
                                    break;
                                case DicomCommandField.CMoveResponse:
                                    _dimse = new DicomCMoveResponse(command);
                                    break;
                                case DicomCommandField.CEchoRequest:
                                    _dimse = new DicomCEchoRequest(command);
                                    break;
                                case DicomCommandField.CEchoResponse:
                                    _dimse = new DicomCEchoResponse(command);
                                    break;
                                case DicomCommandField.NActionRequest:
                                    _dimse = new DicomNActionRequest(command);
                                    break;
                                case DicomCommandField.NActionResponse:
                                    _dimse = new DicomNActionResponse(command);
                                    break;
                                case DicomCommandField.NCreateRequest:
                                    _dimse = new DicomNCreateRequest(command);
                                    break;
                                case DicomCommandField.NCreateResponse:
                                    _dimse = new DicomNCreateResponse(command);
                                    break;
                                case DicomCommandField.NDeleteRequest:
                                    _dimse = new DicomNDeleteRequest(command);
                                    break;
                                case DicomCommandField.NDeleteResponse:
                                    _dimse = new DicomNDeleteResponse(command);
                                    break;
                                case DicomCommandField.NEventReportRequest:
                                    _dimse = new DicomNEventReportRequest(command);
                                    break;
                                case DicomCommandField.NEventReportResponse:
                                    _dimse = new DicomNEventReportResponse(command);
                                    break;
                                case DicomCommandField.NGetRequest:
                                    _dimse = new DicomNGetRequest(command);
                                    break;
                                case DicomCommandField.NGetResponse:
                                    _dimse = new DicomNGetResponse(command);
                                    break;
                                case DicomCommandField.NSetRequest:
                                    _dimse = new DicomNSetRequest(command);
                                    break;
                                case DicomCommandField.NSetResponse:
                                    _dimse = new DicomNSetResponse(command);
                                    break;
                                default:
                                    _dimse = new DicomMessage(command);
                                    break;
                            }
                            _dimse.PresentationContext =
                                Association.PresentationContexts.FirstOrDefault(x => x.ID == pdv.PCID);
                            if (!_dimse.HasDataset)
                            {
                                PerformDimse(this._dimse);
                                _dimse = null;
                                return;
                            }
                        }
                        else
                        {
                            if (_dimse.Type != DicomCommandField.CStoreRequest)
                            {
                                _dimseStream.Seek(0, SeekOrigin.Begin);

                                var pc = Association.PresentationContexts.FirstOrDefault(x => x.ID == pdv.PCID);

                                _dimse.Dataset = new DicomDataset();
                                _dimse.Dataset.InternalTransferSyntax = pc.AcceptedTransferSyntax;

                                var source = new StreamByteSource(_dimseStream);
                                source.Endian = pc.AcceptedTransferSyntax.Endian;

                                var reader = new DicomReader();
                                reader.IsExplicitVR = pc.AcceptedTransferSyntax.IsExplicitVR;
                                reader.Read(source, new DicomDatasetReaderObserver(_dimse.Dataset));

                                _dimseStream = null;
                                _dimseStreamFile = null;
                            }
                            else
                            {
                                var request = _dimse as DicomCStoreRequest;

                                try
                                {
                                    var dicomFile = GetCStoreDicomFile();
                                    _dimseStream = null;
                                    _dimseStreamFile = null;

                                    // NOTE: dicomFile will be valid with the default implementation of CreateCStoreReceiveStream() and
                                    // GetCStoreDicomFile(), but can be null if a child class overrides either method and changes behavior.
                                    // See documentation on CreateCStoreReceiveStream() and GetCStoreDicomFile() for information about why
                                    // this might be desired.
                                    request.File = dicomFile;
                                    if (request.File != null)
                                    {
                                        request.Dataset = request.File.Dataset;
                                    }
                                }
                                catch (Exception e)
                                {
                                    // failed to parse received DICOM file; send error response instead of aborting connection
                                    SendResponse(
                                        new DicomCStoreResponse(
                                            request,
                                            new DicomStatus(DicomStatus.ProcessingFailure, e.Message)));
                                    Logger.Error("Error parsing C-Store dataset: {@error}", e.Message);
                                    (this as IDicomCStoreProvider).OnCStoreRequestException(
                                        _dimseStreamFile != null ? _dimseStreamFile.Name : null, e);
                                    return;
                                }
                            }

                            PerformDimse(this._dimse);
                            _dimse = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception processing P-Data-TF PDU: {@error}", e.Message);
                throw;
            }
            finally
            {
                SendNextMessage();
            }
        }

        private void PerformDimse(DicomMessage dimse)
        {
            Logger.Info("{logId} <- {dicomMessage}", LogID, dimse.ToString(Options.LogDimseDatasets));

            if (!DicomMessage.IsRequest(dimse.Type))
            {
                var rsp = dimse as DicomResponse;
                DicomRequest req;
                lock (_lock)
                {
                    req = _pending.FirstOrDefault(x => x.MessageID == rsp.RequestMessageID);
                }

                if (req != null)
                {
                    rsp.UserState = req.UserState;
                    req.PostResponse(this, rsp);
                    if (rsp.Status.State != DicomState.Pending)
                    {
                        lock (_lock)
                        {
                            _pending.Remove(req);
                        }
                    }
                }

                return;
            }

            if (dimse.Type == DicomCommandField.CStoreRequest)
            {
                if (this is IDicomCStoreProvider)
                {
                    var response = (this as IDicomCStoreProvider).OnCStoreRequest(dimse as DicomCStoreRequest);
                    SendResponse(response);
                    return;
                }
                else if (this is IDicomServiceUser)
                {
                    var response = (this as IDicomServiceUser).OnCStoreRequest(dimse as DicomCStoreRequest);
                    SendResponse(response);
                    return;
                }
                else
                {
                    throw new DicomNetworkException("C-Store SCP not implemented");
                }
            }

            if (dimse.Type == DicomCommandField.CFindRequest)
            {
                if (this is IDicomCFindProvider)
                {
                    var responses = (this as IDicomCFindProvider).OnCFindRequest(dimse as DicomCFindRequest);
                    foreach (var response in responses) SendResponse(response);
                    return;
                }
                else throw new DicomNetworkException("C-Find SCP not implemented");
            }

            if (dimse.Type == DicomCommandField.CGetRequest)
            {
                if (this is IDicomCGetProvider)
                {
                    var responses = (this as IDicomCGetProvider).OnCGetRequest(dimse as DicomCGetRequest);
                    foreach (var response in responses) SendResponse(response);
                    return;
                }
                else throw new DicomNetworkException("C-GET SCP not implemented");
            }

            if (dimse.Type == DicomCommandField.CMoveRequest)
            {
                if (this is IDicomCMoveProvider)
                {
                    var responses = (this as IDicomCMoveProvider).OnCMoveRequest(dimse as DicomCMoveRequest);
                    foreach (var response in responses) SendResponse(response);
                    return;
                }
                else throw new DicomNetworkException("C-Move SCP not implemented");
            }

            if (dimse.Type == DicomCommandField.CEchoRequest)
            {
                if (this is IDicomCEchoProvider)
                {
                    var response = (this as IDicomCEchoProvider).OnCEchoRequest(dimse as DicomCEchoRequest);
                    SendResponse(response);
                    return;
                }
                else throw new DicomNetworkException("C-Echo SCP not implemented");
            }

            if (dimse.Type == DicomCommandField.NActionRequest || dimse.Type == DicomCommandField.NCreateRequest
                || dimse.Type == DicomCommandField.NDeleteRequest
                || dimse.Type == DicomCommandField.NEventReportRequest
                || dimse.Type == DicomCommandField.NGetRequest || dimse.Type == DicomCommandField.NSetRequest)
            {
                if (!(this is IDicomNServiceProvider)) throw new DicomNetworkException("N-Service SCP not implemented");

                DicomResponse response = null;
                if (dimse.Type == DicomCommandField.NActionRequest) response = (this as IDicomNServiceProvider).OnNActionRequest(dimse as DicomNActionRequest);
                else if (dimse.Type == DicomCommandField.NCreateRequest) response = (this as IDicomNServiceProvider).OnNCreateRequest(dimse as DicomNCreateRequest);
                else if (dimse.Type == DicomCommandField.NDeleteRequest)
                    response =
                        (this as IDicomNServiceProvider).OnNDeleteRequest(dimse as DicomNDeleteRequest);
                else if (dimse.Type == DicomCommandField.NEventReportRequest)
                    response =
                        (this as IDicomNServiceProvider).OnNEventReportRequest(
                            dimse as DicomNEventReportRequest);
                else if (dimse.Type == DicomCommandField.NGetRequest)
                    response =
                        (this as IDicomNServiceProvider).OnNGetRequest(dimse as DicomNGetRequest);
                else if (dimse.Type == DicomCommandField.NSetRequest)
                    response =
                        (this as IDicomNServiceProvider).OnNSetRequest(
                            dimse as DicomNSetRequest);

                SendResponse(response);
                return;
            }

            throw new DicomNetworkException("Operation not implemented");
        }

        private void SendMessage(DicomMessage message)
        {
            lock (_lock)
            {
                _msgQueue.Enqueue(message);
            }

            SendNextMessage();
        }

        private void SendNextMessage()
        {
            while (true)
            {
                DicomMessage msg;
                lock (_lock)
                {
                    if (_sending)
                    {
                        break;
                    }

                    if (_msgQueue.Count == 0)
                    {
                        if (_pending.Count == 0) OnSendQueueEmpty();
                        break;
                    }

                    if (Association.MaxAsyncOpsInvoked > 0
                        && _pending.Count(req => req.Type != DicomCommandField.CGetRequest)
                        >= Association.MaxAsyncOpsInvoked)
                    {
                        break;
                    }

                    _sending = true;

                    msg = _msgQueue.Dequeue();

                    if (msg is DicomRequest)
                    {
                        _pending.Add(msg as DicomRequest);
                    }
                }

                DoSendMessage(msg);

                lock (_lock) _sending = false;
            }
        }

        private void DoSendMessage(DicomMessage msg)
        {
            DicomPresentationContext pc;
            if (msg is DicomCStoreRequest)
            {
                pc =
                    Association.PresentationContexts.FirstOrDefault(
                        x =>
                            x.Result == DicomPresentationContextResult.Accept && x.AbstractSyntax == msg.SOPClassUID
                            && x.AcceptedTransferSyntax == (msg as DicomCStoreRequest).TransferSyntax);
                if (pc == null)
                    pc =
                        Association.PresentationContexts.FirstOrDefault(
                            x => x.Result == DicomPresentationContextResult.Accept && x.AbstractSyntax == msg.SOPClassUID);
            }
            else if (msg is DicomResponse)
            {
                //the presentation context should be set already from the request object
                pc = msg.PresentationContext;

                //fail safe if no presentation context is already assigned to the response (is this going to happen)
                if (pc == null)
                {
                    pc =
                        this.Association.PresentationContexts.FirstOrDefault<DicomPresentationContext>(
                            x => (x.Result == DicomPresentationContextResult.Accept) && (x.AbstractSyntax == msg.SOPClassUID));
                }
            }
            else
            {
                pc =
                    Association.PresentationContexts.FirstOrDefault(
                        x => x.Result == DicomPresentationContextResult.Accept && x.AbstractSyntax == msg.SOPClassUID);
            }

            if (pc == null)
            {
                pc = msg.PresentationContext;
            }

            if (pc == null)
            {
                lock (_lock)
                {
                    _pending.Remove(msg as DicomRequest);
                }

                try
                {
                    msg.UserState = "No PresentationContext";
                    if (msg is DicomCStoreRequest)
                        (msg as DicomCStoreRequest).PostResponse(
                            this,
                            new DicomCStoreResponse(msg as DicomCStoreRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomCEchoRequest)
                        (msg as DicomCEchoRequest).PostResponse(
                            this,
                            new DicomCEchoResponse(msg as DicomCEchoRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomCFindRequest)
                        (msg as DicomCFindRequest).PostResponse(
                            this,
                            new DicomCFindResponse(msg as DicomCFindRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomCGetRequest)
                        (msg as DicomCGetRequest).PostResponse(
                            this,
                            new DicomCGetResponse(msg as DicomCGetRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomCMoveRequest)
                        (msg as DicomCMoveRequest).PostResponse(
                            this,
                            new DicomCMoveResponse(msg as DicomCMoveRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNActionRequest)
                        (msg as DicomNActionRequest).PostResponse(
                            this,
                            new DicomNActionResponse(msg as DicomNActionRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNCreateRequest)
                        (msg as DicomNCreateRequest).PostResponse(
                            this,
                            new DicomNCreateResponse(msg as DicomNCreateRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNDeleteRequest)
                        (msg as DicomNDeleteRequest).PostResponse(
                            this,
                            new DicomNDeleteResponse(msg as DicomNDeleteRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNEventReportRequest)
                        (msg as DicomNEventReportRequest).PostResponse(
                            this,
                            new DicomNEventReportResponse(msg as DicomNEventReportRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNGetRequest)
                        (msg as DicomNGetRequest).PostResponse(
                            this,
                            new DicomNGetResponse(msg as DicomNGetRequest, DicomStatus.SOPClassNotSupported));
                    else if (msg is DicomNSetRequest)
                        (msg as DicomNSetRequest).PostResponse(
                            this,
                            new DicomNSetResponse(msg as DicomNSetRequest, DicomStatus.SOPClassNotSupported));
                    else
                    {
                        Logger.Warn("Unknown message type: {type}", msg.Type);
                    }
                }
                catch
                {
                }

                Logger.Error("No accepted presentation context found for abstract syntax: {sopClassUid}", msg.SOPClassUID);
            }
            else
            {
                if(msg is DicomRequest)
                {
                    DicomRequest req = msg as DicomRequest;
                    if (req.OnBeforeSendRequest != null) req.OnBeforeSendRequest();
                }

                var dimse = new Dimse { Message = msg, PresentationContext = pc };

                // force calculation of command group length as required by standard
                msg.Command.RecalculateGroupLengths();

                if (msg.HasDataset)
                {
                    // remove group lengths as recommended in PS 3.5 7.2
                    //
                    //	2. It is recommended that Group Length elements be removed during storage or transfer 
                    //	   in order to avoid the risk of inconsistencies arising during coercion of data 
                    //	   element values and changes in transfer syntax.
                    msg.Dataset.RemoveGroupLengths();

                    if (msg.Dataset.InternalTransferSyntax != dimse.PresentationContext.AcceptedTransferSyntax) msg.Dataset = msg.Dataset.Clone(dimse.PresentationContext.AcceptedTransferSyntax);
                }

                if (msg is DicomCStoreRequest)
                {
                    Logger.Debug("{logId} -> {dicomMessage} starting", LogID, msg.ToString());
                }

                try
                {
                    dimse.Stream = new PDataTFStream(this, pc.ID, Association.MaximumPDULength);

                    var writer = new DicomWriter(
                        DicomTransferSyntax.ImplicitVRLittleEndian,
                        DicomWriteOptions.Default,
                        new StreamByteTarget(dimse.Stream));

                    dimse.Walker = new DicomDatasetWalker(msg.Command);
                    dimse.Walker.Walk(writer);

                    if (dimse.Message.HasDataset)
                    {
                        dimse.Stream.SetIsCommandAsync(false).Wait();

                        writer = new DicomWriter(
                            dimse.PresentationContext.AcceptedTransferSyntax,
                            DicomWriteOptions.Default,
                            new StreamByteTarget(dimse.Stream));

                        dimse.Walker = new DicomDatasetWalker(dimse.Message.Dataset);
                        dimse.Walker.Walk(writer);
                    }
                    Logger.Info("{logId} -> {dicomMessage}", LogID, msg.ToString(Options.LogDimseDatasets));
                }
                catch (Exception e)
                {
                    Logger.Error("Exception sending DIMSE: {@error}", e);
                }
                finally
                {
                    dimse.Stream.FlushAsync(true).Wait();
                    dimse.Stream.Dispose();
                }
            }
        }

        private bool TryCloseConnection(Exception exception = null, bool force = false)
        {
            try
            {
                if (!IsConnected) return true;

                lock (_lock)
                {
                    if (force)
                    {
                        _pduQueue.Clear();
                        _msgQueue.Clear();
                        _pending.Clear();
                        _pduQueueWatcher.Set();
                    }

                    if (_pduQueue.Count > 0 || _msgQueue.Count > 0 || _pending.Count > 0)
                    {
                        Logger.Warn(
                            "Queue(s) not empty, PDUs: {pduCount}, messages: {msgCount}, pending requests: {pendingCount}",
                            _pduQueue.Count,
                            _msgQueue.Count,
                            _pending.Count);
                        return false;
                    }
                }
                string logMsg = "";
                if (LogID != null) logMsg = LogID;
                if (this is IDicomServiceUser)
                {
                    if (this.Association != null)
                    {
                        logMsg += " -> ";
                    }
                    logMsg += "DicomSCU";
                }
                else
                {
                    if (this.Association != null)
                    {
                        logMsg += " -> ";
                    }
                    logMsg += "DicomSCP";
                }
                logMsg += " connection ending, Association state: " + AssociationState.ToString();
                if (exception != null)
                {
                    logMsg += "; Error: ";
                    if (exception.InnerException != null)
                    {
                        logMsg += exception.InnerException.Message;
                    }
                    else
                    {
                        logMsg += exception.Message;
                    }
                }
                if(AssociationState != DicomAssociationState.Released || exception != null)
                {
                    Logger.Warn(logMsg);
                }
                else
                {
                    Logger.Info(logMsg);
                }
                CalcSpeed(0, 0, false, true);
                lock (_lock) IsConnected = false;
                (this as IDicomService)?.OnConnectionClosed(exception);

                //throwing here just bubbles out... why do that?
                //if (exception != null) throw exception;
                return true;
            }
            catch (Exception e)
            {
                Logger.Error("Error during close attempt: {@error}", e.Message);
                throw;
            }
        }

        #endregion

        #region Send Methods

        /// <summary>
        /// Send association request.
        /// </summary>
        /// <param name="association">DICOM association.</param>
        protected void SendAssociationRequest(DicomAssociation association)
        {
            LogID = association.CalledAE + " (" + association.AssociationId.ToString() + ")"; ;
            if (Options.UseRemoteAEForLogName) Logger = LogManager.GetLogger(LogID);
            Logger.Info("{calledAE} -> Association request:\n{association}", LogID, association.ToString());
            Association = association;
            AssociationState = DicomAssociationState.Requested;
            this.SendPDUAsync(new AAssociateRQ(Association)).Wait();
        }

        /// <summary>
        /// Send association accept response.
        /// </summary>
        /// <param name="association">DICOM association.</param>
        protected void SendAssociationAccept(DicomAssociation association)
        {
            Association = association;

            // reject all presentation contexts that have not already been accepted or rejected
            foreach (var pc in Association.PresentationContexts)
            {
                if (pc.Result == DicomPresentationContextResult.Proposed) pc.SetResult(DicomPresentationContextResult.RejectNoReason);
            }

            Logger.Info("{logId} -> Association accept:\n{association}", LogID, association.ToString());
            AssociationState = DicomAssociationState.Accepted;
            this.SendPDUAsync(new AAssociateAC(Association)).Wait();
        }

        /// <summary>
        /// Send association reject response.
        /// </summary>
        /// <param name="result">Rejection result.</param>
        /// <param name="source">Rejection source.</param>
        /// <param name="reason">Rejection reason.</param>
        protected void SendAssociationReject(
            DicomRejectResult result,
            DicomRejectSource source,
            DicomRejectReason reason)
        {
            Logger.Info(
                "{logId} -> Association reject [result: {result}; source: {source}; reason: {reason}]",
                LogID,
                result,
                source,
                reason);
            AssociationState = DicomAssociationState.Rejected;
            this.SendPDUAsync(new AAssociateRJ(result, source, reason)).Wait();
            if (!TryCloseConnection())
            {
                Logger.Warn("{logId} -> Could not close connection after sending AssociationReject", LogID);
            }
        }

        /// <summary>
        /// Send association release request.
        /// </summary>
        protected void SendAssociationReleaseRequest()
        {
            Logger.Info("{logId} -> Association release request", LogID);
            AssociationState = DicomAssociationState.ReleaseRequested;
            this.SendPDUAsync(new AReleaseRQ()).Wait();
        }

        /// <summary>
        /// Send association release response.
        /// </summary>
        protected void SendAssociationReleaseResponse()
        {
            Logger.Info("{logId} -> Association release response", LogID);
            AssociationState = DicomAssociationState.Released;
            this.SendPDUAsync(new AReleaseRP()).Wait();
        }

        /// <summary>
        /// Send abort request.
        /// </summary>
        /// <param name="source">Abort source.</param>
        /// <param name="reason">Abort reason.</param>
        protected void SendAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            _pduQueue.Clear();
            _msgQueue.Clear();
            _pending.Clear();
            Logger.Warn("{logId} -> Abort [source: {source}; reason: {reason}]", LogID, source, reason);
            this.SendPDUAsync(new AAbort(source, reason)).Wait();
        }

        #endregion

        #region Override Methods

        /// <summary>
        /// Action to perform when send queue is empty.
        /// </summary>
        protected virtual void OnSendQueueEmpty()
        {
        }

        #endregion

        #region Helper methods

        private bool LogIOException(Exception e, Logger logger, bool reading)
        {
            int errorCode;
            string errorDescriptor;
            if (NetworkManager.IsSocketException(e.InnerException, out errorCode, out errorDescriptor))
            {
                string logMsg = "";
                if (LogID != null) logMsg = LogID;
                string state = AssociationState.ToString();
                if (this.AssociationState != DicomAssociationState.Released || errorCode != 10054)
                {
                    logger.Warn(logMsg +
                        $" Socket error while {(reading ? "reading" : "writing")} PDU: {{socketError}} [{{errorCode}}], Association state: {{state}}",
                        errorDescriptor, errorCode, state);
                }
                else
                {
                    logger.Info(logMsg +
                        $" Socket exception while {(reading ? "reading" : "writing")} PDU: {{socketError}} [{{errorCode}}], Association state: {{state}}",
                        errorDescriptor, errorCode, state);
                }
                return true;
            }

            if (e.InnerException is ObjectDisposedException)
            {
                logger.Warn($"Object disposed while {(reading ? "reading" : "writing")} PDU: {{@error}}", e);
            }
            else
            {
                logger.Error($"I/O exception while {(reading ? "reading" : "writing")} PDU: {{@error}}", e);
            }

            return false;
        }

        #endregion

        #region INNER TYPES

        private class Dimse
        {
            public DicomMessage Message;

            public PDataTFStream Stream;

            public DicomDatasetWalker Walker;

            public DicomPresentationContext PresentationContext;
        }

        private class PDataTFStream : Stream
        {
            #region Private Members

            private readonly DicomService _service;

            private bool _command;

            private readonly uint _pduMax;

            private uint _max;

            private readonly byte _pcid;

            private PDataTF _pdu;

            private byte[] _bytes;

            private int _length;

            #endregion

            #region Public Constructors

            public PDataTFStream(DicomService service, byte pcid, uint max)
            {
                _service = service;
                _command = true;
                _pcid = pcid;
                _pduMax = Math.Min(max, Int32.MaxValue);
                _max = (_pduMax == 0)
                           ? _service.Options.MaxCommandBuffer
                           : Math.Min(_pduMax, _service.Options.MaxCommandBuffer);

                _pdu = new PDataTF();

                // Max PDU Size - Current Size - Size of PDV header
                _bytes = new byte[_max - CurrentPduSize() - 6];
            }

            #endregion

            #region Public Properties

            public async Task SetIsCommandAsync(bool value)
            {
                // recalculate maximum PDU buffer size
                if (_command != value)
                {
                    _max = _pduMax == 0
                               ? _service.Options.MaxCommandBuffer
                               : Math.Min(
                                   _pduMax,
                                   value ? _service.Options.MaxCommandBuffer : _service.Options.MaxDataBuffer);

                    await CreatePDVAsync(true).ConfigureAwait(false);
                    _command = value;
                }
            }

            #endregion

            #region Public Members

            public async Task FlushAsync(bool last)
            {
                await CreatePDVAsync(last).ConfigureAwait(false);
                await WritePDUAsync(last).ConfigureAwait(false);
            }

            #endregion

            #region Private Members

            private uint CurrentPduSize()
            {
                // PDU header + PDV header + PDV data
                return 6 + _pdu.GetLengthOfPDVs();
            }

            private async Task CreatePDVAsync(bool last)
            {
                try
                {
                    if (_bytes == null) _bytes = new byte[0];

                    if (_length < _bytes.Length) Array.Resize(ref _bytes, _length);

                    PDV pdv = new PDV(_pcid, _bytes, _command, last);
                    _pdu.PDVs.Add(pdv);

                    // reset length in case we recurse into WritePDU()
                    _length = 0;
                    // is the current PDU at its maximum size or do we have room for another PDV?
                    if ((CurrentPduSize() + 6) >= _max || (!_command && last)) await WritePDUAsync(last).ConfigureAwait(false);

                    // Max PDU Size - Current Size - Size of PDV header
                    uint max = _max - CurrentPduSize() - 6;
                    _bytes = last ? null : new byte[max];
                }
                catch (Exception e)
                {
                    _service.Logger.Error("Exception creating PDV: {@error}", e);
                    throw;
                }
            }

            private async Task WritePDUAsync(bool last)
            {
                if (_length > 0) await CreatePDVAsync(last).ConfigureAwait(false);

                if (_pdu.PDVs.Count > 0)
                {
                    if (last) _pdu.PDVs[_pdu.PDVs.Count - 1].IsLastFragment = true;

                    await _service.SendPDUAsync(_pdu).ConfigureAwait(false);

                    _pdu = new PDataTF();
                }
            }

            #endregion

            #region Stream Members

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override void Flush()
            {
            }

            public override long Length
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                try
                {
                    if (_bytes == null || _bytes.Length == 0)
                    {
                        // Max PDU Size - Current Size - Size of PDV header
                        uint max = _max - CurrentPduSize() - 6;
                        _bytes = new byte[max];
                    }

                    while (count >= (_bytes.Length - _length))
                    {
                        int c = Math.Min(count, _bytes.Length - _length);

                        Array.Copy(buffer, offset, _bytes, _length, c);

                        _length += c;
                        offset += c;
                        count -= c;

                        CreatePDVAsync(false).Wait();
                    }

                    if (count > 0)
                    {
                        Array.Copy(buffer, offset, _bytes, _length, count);
                        _length += count;

                        if (_bytes.Length == _length) CreatePDVAsync(false).Wait();
                    }
                }
                catch (Exception e)
                {
                    _service.Logger.Error("Exception writing data to PDV: {@error}", e);
                    throw;
                }
            }

            #endregion
        }

        #endregion
    }
}