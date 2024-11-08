﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using Dicom.Imaging.Codec;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicom.Network
{
    /// <summary>
    /// Collection of presentation contexts, with unique ID:s.
    /// </summary>
    public class DicomPresentationContextCollection : ICollection<DicomPresentationContext>
    {
        #region FIELDS

        private readonly object locker = new object();

        private readonly SortedDictionary<byte, DicomPresentationContext> _pc;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes a new instance of <see cref="DicomPresentationContextCollection"/>.
        /// </summary>
        public DicomPresentationContextCollection()
        {
            _pc = new SortedDictionary<byte, DicomPresentationContext>();
        }

        #endregion

        #region INDEXERS

        /// <summary>
        /// Gets the presentation context associated with <paramref name="id"/>.
        /// </summary>
        /// <param name="id">Presentation context ID.</param>
        /// <returns>Presentation context associated with <paramref name="id"/></returns>
        public DicomPresentationContext this[byte id] => _pc[id];

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the number of presentation contexts in collection.
        /// </summary>
        public int Count => _pc.Count;

        /// <summary>
        /// Gets whether collection is read-only. Is always <code>false</code>.
        /// </summary>
        public bool IsReadOnly => false;

        #endregion

        #region METHODS

        /// <summary>
        /// Initialize and add new presentation context.
        /// </summary>
        /// <param name="abstractSyntax">Abstract syntax of the presentation context.</param>
        /// <param name="transferSyntaxes">Supported transfer syntaxes.</param>
        public void Add(DicomUID abstractSyntax, params DicomTransferSyntax[] transferSyntaxes)
        {
            Add(abstractSyntax, null, null, transferSyntaxes);
        }

        /// <summary>
        /// Initialize and add new presentation context.
        /// </summary>
        /// <param name="abstractSyntax">Abstract syntax of the presentation context.</param>
        /// <param name="userRole">Supports SCU role?</param>
        /// <param name="providerRole">Supports SCP role?</param>
        /// <param name="transferSyntaxes">Supported transfer syntaxes.</param>
        public void Add(DicomUID abstractSyntax, bool? userRole, bool? providerRole, params DicomTransferSyntax[] transferSyntaxes)
        {
            var pc = new DicomPresentationContext(GetNextPresentationContextID(), abstractSyntax, userRole, providerRole);

            foreach (var tx in transferSyntaxes) pc.AddTransferSyntax(tx);

            Add(pc);
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
        /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public void Add(DicomPresentationContext item)
        {
            _pc.Add(item.ID, item);
        }

        /// <summary>
        /// Add presentation contexts obtained from DICOM request.
        /// </summary>
        /// <param name="request">Request from which presentation context(s) should be obtained.</param>
        public void AddFromRequest(DicomRequest request)
        {
            if (request is DicomCStoreRequest)
            {
                var cstore = request as DicomCStoreRequest;
                DicomPresentationContext pc = UpdateExistingPresentationContext(cstore);
                if (pc == null)
                {
                    bool canTranscode = (!cstore.TransferSyntax.IsEncapsulated) || TranscoderManager.HasCodec(cstore.TransferSyntax);
                    IList<DicomTransferSyntax> csptx = null;
                    //see if the request explicitly define presentation context
                    if (canTranscode && cstore.PresentationContext != null && cstore.PresentationContext.AbstractSyntax == cstore.SOPClassUID)
                    {
                        csptx = cstore.PresentationContext.GetTransferSyntaxes();
                    }
                    var tx = new List<DicomTransferSyntax>();

                    if (csptx != null)
                    {
                        foreach (DicomTransferSyntax pts in csptx)
                        {
                            //check if we can transcode TO the syntax...
                            bool tsOkay = (!pts.IsEncapsulated) || TranscoderManager.HasCodec(pts);
                            if (tsOkay && !tx.Contains(pts)) tx.Add(pts);
                        }
                    }
                    if (tx.Count == 0)
                    {
                        tx.Add(cstore.TransferSyntax);
                    }
                    if (canTranscode)
                    {
                        if (cstore.AdditionalTransferSyntaxes != null)
                        {
                            foreach (var ats in cstore.AdditionalTransferSyntaxes)
                            {
                                bool tsOkay = (!ats.IsEncapsulated) || TranscoderManager.HasCodec(ats);
                                if (tsOkay && !tx.Contains(ats)) tx.Add(ats);
                            }
                        }
                        //moved to cstore initialization
                        //if (!tx.Contains(DicomTransferSyntax.ExplicitVRLittleEndian)) tx.Add(DicomTransferSyntax.ExplicitVRLittleEndian);
                        //if (!tx.Contains(DicomTransferSyntax.ImplicitVRLittleEndian)) tx.Add(DicomTransferSyntax.ImplicitVRLittleEndian);
                    }
                    if (tx.Count > 0) Add(cstore.SOPClassUID, tx.ToArray());
                }
            }
            else
            {
                if (request.PresentationContext != null)
                {
                    var pc =
                        _pc.Values.FirstOrDefault(
                            x =>
                                x.AbstractSyntax == request.PresentationContext.AbstractSyntax &&
                                request.PresentationContext.GetTransferSyntaxes().All(y => x.GetTransferSyntaxes().Contains(y)));

                    if (pc == null)
                    {
                        var transferSyntaxes = request.PresentationContext.GetTransferSyntaxes().ToArray();
                        if (!transferSyntaxes.Any())
                            transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian, DicomTransferSyntax.ImplicitVRLittleEndian };
                        Add(
                            request.PresentationContext.AbstractSyntax,
                            request.PresentationContext.UserRole,
                            request.PresentationContext.ProviderRole,
                            transferSyntaxes);
                    }
                }
                else
                {
                    var pc = _pc.Values.FirstOrDefault(x => x.AbstractSyntax == request.SOPClassUID);
                    if (pc == null)
                        Add(
                            request.SOPClassUID,
                            DicomTransferSyntax.ExplicitVRLittleEndian,
                            DicomTransferSyntax.ImplicitVRLittleEndian);
                }
            }
        }

        private DicomPresentationContext UpdateExistingPresentationContext(DicomCStoreRequest cstore)
        {
            DicomPresentationContext pc = null;
            bool canTranscode = (!cstore.TransferSyntax.IsEncapsulated) || TranscoderManager.HasCodec(cstore.TransferSyntax); ;
            //this checks against existing presentation contexts
            var pcs = _pc.Values.Where(x => x.AbstractSyntax == cstore.SOPClassUID);
            //see if the request explicitly define presentation context
            if (canTranscode)
            {
                IList<DicomTransferSyntax> csptx = null;
                if (cstore.PresentationContext != null && cstore.PresentationContext.AbstractSyntax == cstore.SOPClassUID)
                {
                    csptx = cstore.PresentationContext.GetTransferSyntaxes();
                }
                else
                {
                    csptx = new DicomTransferSyntax[] { cstore.TransferSyntax };
                }
                foreach (var pv in pcs)
                {
                    var pvtxs = pv.GetTransferSyntaxes();
                    var matches = pvtxs.Intersect(csptx);
                    if (matches.FirstOrDefault() != null)
                    {
                        pc = pv;
                        var pctx = pc.GetTransferSyntaxes();
                        var diff = pvtxs.Except(csptx);
                        foreach (var dp in diff)
                        {
                            bool tsOkay = (!dp.IsEncapsulated) || TranscoderManager.HasCodec(dp);
                            //assumes each transferSyntax in diff is unique
                            if (tsOkay && !pctx.Contains(dp)) pc.AddTransferSyntax(dp);
                        }
                        break;
                    }
                }
            }
            else
            {
                if (cstore.TransferSyntax == DicomTransferSyntax.ImplicitVRLittleEndian)
                {
                    pcs = pcs.Where(x => x.GetTransferSyntaxes().Contains(DicomTransferSyntax.ImplicitVRLittleEndian));
                }
                else
                {
                    //this just gets the presentationcontext where the first one equals the cstore
                    //assumes the first one is the cstore? 
                    //could be smarter - find a presentationcontext with a *compatible* transfersyntax
                    //force cstore.TransferSyntax if unable to transcode
                    pcs = pcs.Where(x => x.AcceptedTransferSyntax == cstore.TransferSyntax);
                }
                pc = pcs.FirstOrDefault();//this is just a check to see if it found something
            }
            return pc;
        }

        /// <summary>
        /// Clear all presentation contexts in collection.
        /// </summary>
        public void Clear()
        {
            _pc.Clear();
        }

        /// <summary>
        /// Indicates if specified presentation context is contained in collection.
        /// </summary>
        /// <param name="item">Presentation context to search for.</param>
        /// <returns><code>true</code> if <paramref name="item"/> is contained in collection, <code>false</code> otherwise.</returns>
        public bool Contains(DicomPresentationContext item)
        {
            return _pc.ContainsKey(item.ID) && _pc[item.ID].AbstractSyntax == item.AbstractSyntax;
        }

        public bool ContainsKey(byte itemId)
        {
            return _pc.ContainsKey(itemId);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception><exception cref="T:System.ArgumentException">The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
        void ICollection<DicomPresentationContext>.CopyTo(DicomPresentationContext[] array, int arrayIndex)
        {
            throw new NotSupportedException("Not implemented");
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </summary>
        /// <returns>
        /// true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>.
        /// </returns>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
        public bool Remove(DicomPresentationContext item)
        {
            return _pc.Remove(item.ID);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<DicomPresentationContext> GetEnumerator()
        {
            return _pc.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets a unique presentation context ID.
        /// </summary>
        /// <returns>Unique presentation context ID.</returns>
        private byte GetNextPresentationContextID()
        {
            lock (locker)
            {
                if (_pc.Count == 0) return 1;

                var id = _pc.Max(x => x.Key) + 2;

                if (id >= 256) throw new DicomNetworkException("Too many presentation contexts configured for this association!");

                return (byte)id;
            }
        }

        #endregion
    }
}
