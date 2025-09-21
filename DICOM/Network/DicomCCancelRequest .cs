
namespace Dicom.Network
{
    using Dicom;
    using System;
    /// <summary>
    /// DICOM C-CANCEL request for a corresponding C-GET, C-FIND, or C-MOVE request.
    /// </summary>
    public sealed class DicomCCancelRequest : DicomPriorityRequest
    {

        public ushort RequestToCancelMessageID
        {
            get{
                return Dataset.Get<ushort>(DicomTag.MessageIDBeingRespondedTo, 0, 0);
            }
        }
        /// <inheritdoc />
        private DicomCCancelRequest(DicomRequest requestToCancel)
            : base(DicomCommandField.CCancelRequest, requestToCancel.SOPClassUID, DicomPriority.High)
        {
            //outgoing
            Dataset = new DicomDataset();
            Dataset.AddOrUpdate(DicomTag.MessageIDBeingRespondedTo, requestToCancel.MessageID);
        }
        /// <summary>
        /// Inbound constructor
        /// </summary>
        /// <param name="command"></param>
        public DicomCCancelRequest(DicomDataset command) : base(command)
        {
        }

        /// <inheritdoc />
        protected internal override void PostResponse(DicomService service, DicomResponse response)
        {
            // empty on purpose, as there is no response to this message
        }

        /// <summary>
        /// Creates a corresponding C-CANCEL request for the given C-MOVE request..
        /// </summary>
        public static DicomCCancelRequest Create(DicomCMoveRequest request)
        {
            return new DicomCCancelRequest(request);
        }

        /// <summary>
        /// Creates a corresponding C-CANCEL request for the given C-FIND request..
        /// </summary>
        public static DicomCCancelRequest Create(DicomCFindRequest request)
        {
            return new DicomCCancelRequest(request);
        }

        /// <summary>
        /// Creates a corresponding C-CANCEL request for the given C-GET request..
        /// </summary>
        public static DicomCCancelRequest Create(DicomCGetRequest request)
        {
            return new DicomCCancelRequest(request);
        }
    }
}
