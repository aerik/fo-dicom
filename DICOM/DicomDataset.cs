﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom
{
    using Dicom.IO.Buffer;
    using Dicom.StructuredReport;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;


    /// <summary>
    /// A collection of <see cref="DicomItem">DICOM items</see>.
    /// </summary>
    public class DicomDataset : IEnumerable<DicomItem>
    {
        #region FIELDS

        private readonly IDictionary<DicomTag, DicomItem> _items;

        private DicomTransferSyntax _syntax;

        private bool _isDisposed = false; // To detect redundant calls

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Initializes a new instance of the <see cref="DicomDataset"/> class.
        /// </summary>
        public DicomDataset()
        {
            _items = new SortedDictionary<DicomTag, DicomItem>();
            InternalTransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DicomDataset"/> class.
        /// </summary>
        /// <param name="items">An array of DICOM items.</param>
        public DicomDataset(params DicomItem[] items)
            : this((IEnumerable<DicomItem>)items)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DicomDataset"/> class.
        /// </summary>
        /// <param name="items">A collection of DICOM items.</param>
        public DicomDataset(IEnumerable<DicomItem> items, bool validate = false)
            : this()
        {
            ValidateItems = validate;
            if (items != null)
            {
                foreach (var item in items.Where(item => item != null))
                {
                    if (item.ValueRepresentation.Equals(DicomVR.SQ))
                    {
                        var tag = item.Tag;
                        var sequenceItems =
                            ((DicomSequence)item).Items.Where(dataset => dataset != null)
                                .Select(dataset => new DicomDataset(dataset))
                                .ToArray();
                        _items[tag] = new DicomSequence(tag, sequenceItems);
                    }
                    else
                    {
                        if (ValidateItems)
                        {
                            item.Validate();
                        }
                        _items[item.Tag] = item;
                    }
                }
            }
        }

        #endregion

        #region PROPERTIES

        /// <summary>Gets the DICOM transfer syntax of this dataset.</summary>
        public DicomTransferSyntax InternalTransferSyntax
        {
            get
            {
                return _syntax;
            }
            internal set
            {
                _syntax = value;

                // update transfer syntax for sequence items
                foreach (var sq in this.Where(x => x.ValueRepresentation == DicomVR.SQ).Cast<DicomSequence>())
                {
                    foreach (var item in sq.Items)
                    {
                        item.InternalTransferSyntax = _syntax;
                    }
                }
            }
        }

        internal bool _validateItems = false;//new feature, being careful with this
        internal bool ValidateItems
        {
            get => _validateItems && DicomValidation.PerformValidation;
            set => _validateItems = value;
        }
        /// <summary>
        /// Gets or sets if the content of DicomItems shall be validated as soon as they are added to the DicomDataset
        /// </summary>
        [Obsolete("Use this property with care. You can suppress validation, but be aware you might create invalid Datasets if you need to set this property.", false)]
        public bool AutoValidate
        {
            get => _validateItems;
            set => ValidateItems = value;
        }
        #endregion

        #region METHODS

        /// <summary>
        /// Gets the item or element value of the specified <paramref name="tag"/>. 
        /// </summary>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="n">Item index (for multi-valued elements).</param>
        /// <returns>Item or element value corresponding to <paramref name="tag"/>.</returns>
        /// <exception cref="DicomDataException">If the dataset does not contain <paramref name="tag"/> or if the specified 
        /// <paramref name="n">item index</paramref> is out-of-range.</exception>
        public T Get<T>(DicomTag tag, int n = 0)
        {
            return Get<T>(tag, n, false, default(T));
        }

        /// <summary>
        /// Gets the integer element value of the specified <paramref name="tag"/>, or default value if dataset does not contain <paramref name="tag"/>. 
        /// </summary>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="defaultValue">Default value to apply if <paramref name="tag"/> is not contained in dataset.</param>
        /// <returns>Element value corresponding to <paramref name="tag"/>.</returns>
        /// <exception cref="DicomDataException">If the element corresponding to <paramref name="tag"/> cannot be converted to an integer.</exception>
        public int Get(DicomTag tag, int defaultValue)
        {
            return Get<int>(tag, 0, true, defaultValue);
        }

        /// <summary>
        /// Gets the item or element value of the specified <paramref name="tag"/>, or default value if dataset does not contain <paramref name="tag"/>. 
        /// </summary>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="defaultValue">Default value to apply if <paramref name="tag"/> is not contained in dataset.</param>
        /// <returns>Item or element value corresponding to <paramref name="tag"/>.</returns>
        /// <remarks>In code, consider to use this method with implicit type specification, since <typeparamref name="T"/> can be inferred from
        /// <paramref name="defaultValue"/>, e.g. prefer <code>dataset.Get(tag, "Default")</code> over <code>dataset.Get&lt;string&gt;(tag, "Default")</code>.</remarks>
        public T Get<T>(DicomTag tag, T defaultValue)
        {
            return Get<T>(tag, 0, true, defaultValue);
        }

        /// <summary>
        /// Gets the item or element value of the specified <paramref name="tag"/>, or default value if dataset does not contain <paramref name="tag"/>. 
        /// </summary>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="n">Item index (for multi-valued elements).</param>
        /// <param name="defaultValue">Default value to apply if <paramref name="tag"/> is not contained in dataset.</param>
        /// <returns>Item or element value corresponding to <paramref name="tag"/>.</returns>
        public T Get<T>(DicomTag tag, int n, T defaultValue)
        {
            return Get<T>(tag, n, true, defaultValue);
        }
        /// <summary>
        /// Gets the element value of the specified <paramref name="tag"/>, whose value multiplicity has to be 1.
        /// </summary>
        /// <typeparam name="T">Type of the return value. This cannot be an array type.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <returns>Element values corresponding to <paramref name="tag"/>.</returns>
        /// <exception cref="DicomDataException">If the dataset does not contain <paramref name="tag"/>, is empty or is multi-valued.</exception>
        public T GetSingleValue<T>(DicomTag tag)
        {
            if (typeof(T).GetTypeInfo().IsArray) { throw new DicomDataException("T can't be an Array type. Use GetValues instead"); }

            tag = ValidatePrivate(tag);
            ValidateDicomTag(tag, out DicomItem item);

            if (item is DicomElement element)
            {
                if (typeof(IByteBuffer).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo())) { return (T)(object)element.Buffer; }

                if (element.Count != 1) { throw new DicomDataException($"DICOM element {tag} must contain a single value, but contains {element.Count}"); }

                return element.Get<T>(0);
            }
            else
            {
                throw new DicomDataException("DicomTag doesn't support values.");
            }
        }

        /// <summary>
        /// Tries to get the element value of the specified <paramref name="tag"/>, whose value multiplicity has to be 1.
        /// </summary>
        /// <typeparam name="T">Type of the return value. This cannot be an array type.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="elementValue">Element value corresponding to <paramref name="tag"/>.</param>
        /// <returns>Returns <code>true</code> if the element values could be exctracted, otherwise <code>false</code>.</returns>
        public bool TryGetSingleValue<T>(DicomTag tag, out T value)
        {
            if (typeof(T).GetTypeInfo().IsArray)
            {
                value = default(T);
                return false;
            }

            if (!TryValidatePrivate(ref tag))
            {
                value = default(T);
                return false;
            }
            if (!_items.TryGetValue(tag, out DicomItem item))
            {
                value = default(T);
                return false;
            }

            if (item is DicomElement element && element.Count == 1)
            {
                try
                {
                    value = element.Get<T>(0);
                    return true;
                }
                catch
                {
                    value = default(T);
                    return false;
                }
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        private DicomTag ValidatePrivate(DicomTag tag)
        {
            if (TryValidatePrivate(ref tag))
            {
                return tag;
            }
            else
            {
                throw new DicomDataException($"Tag: {tag} not found in dataset");
            }
        }

        private bool TryValidatePrivate(ref DicomTag tag)
        {
            if (tag.IsPrivate)
            {
                var privateTag = GetPrivateTag(tag, false);
                if (privateTag == null)
                {
                    return false;
                }
                tag = privateTag;
            }
            return true;
        }

        private void ValidateDicomTag(DicomTag tag, out DicomItem item)
        {
            if (!_items.TryGetValue(tag, out item))
            {
                throw new DicomDataException($"Tag: {tag} not found in dataset");
            }
        }

        ///// <summary>
        ///// Converts a dictionary tag to a valid private tag. Creates the private creator tag if needed.
        ///// </summary>
        ///// <param name="tag">Dictionary DICOM tag</param>
        ///// <returns>Private DICOM tag</returns>
        //public DicomTag GetPrivateTag(DicomTag tag)
        //{
        //    // not a private tag
        //    if (!tag.IsPrivate) return tag;

        //    // group length
        //    if (tag.Element == 0x0000) return tag;

        //    // private creator?
        //    if (tag.PrivateCreator == null) return tag;

        //    // already a valid private tag
        //    if (tag.Element >= 0xff) return tag;

        //    ushort group = 0x0010;
        //    for (; ; group++)
        //    {
        //        var creator = new DicomTag(tag.Group, group);
        //        if (!Contains(creator))
        //        {
        //            Add(new DicomLongString(creator, tag.PrivateCreator.Creator));
        //            break;
        //        }

        //        var value = Get(creator, string.Empty);
        //        if (tag.PrivateCreator.Creator == value) return new DicomTag(tag.Group, (ushort)((group << 8) + (tag.Element & 0xff)), tag.PrivateCreator);
        //    }

        //    return new DicomTag(tag.Group, (ushort)((group << 8) + (tag.Element & 0xff)), tag.PrivateCreator);
        //}

        /// <summary>
        /// Converts a dictionary tag to a valid private tag. Creates the private creator tag if needed.
        /// </summary>
        /// <param name="tag">Dictionary DICOM tag</param>
        /// <returns>Private DICOM tag, or null if all groups are already used.</returns>
        public DicomTag GetPrivateTag(DicomTag tag)
        {
            return GetPrivateTag(tag, true);
        }

        /// <summary>
        /// Converts a dictionary tag to a valid private tag.
        /// </summary>
        /// <param name="tag">Dictionary DICOM tag</param>
        /// <param name="createTag">Whether the PrivateCreator tag should be created if needed.</param>
        /// <returns>Private DICOM tag, or null if all groups are already used or createTag is false and the
        /// PrivateCreator is not already in the dataset. </returns>
        internal DicomTag GetPrivateTag(DicomTag tag, bool createTag)
        {
            // not a private tag
            if (!tag.IsPrivate) return tag;//checks if the tag group is odd

            // group length
            if (tag.Element == 0x0000) return tag;

            // this is potentially the private creator tag itself
            if (tag.PrivateCreator == null) return tag;

            if (tag.Element > 0xff)
            {
                if (createTag)
                {
                    //check if creator tag exists
                    ushort creatorPrefix = (ushort)(tag.Element >> 8);
                    DicomTag creatorTag = new DicomTag(tag.Group, creatorPrefix);//don't set PrivateCreator here?
                    if (!Contains(creatorTag))
                    {
                        Add(new DicomLongString(creatorTag, tag.PrivateCreator.Creator));//writes the creator tag to the dataset
                    }
                    else
                    {
                        //what if there is a mismatch?
                        string creatorString = TryGetSingleValue(creatorTag, out string tmpValue) ? tmpValue : string.Empty;
                        if (creatorString != tag.PrivateCreator.Creator)
                        {
                            //uh-oh... ignore it?
                        }
                    }
                }
                return tag;
            }

            // already a valid private tag
            //if (tag.Element > 0xff) return tag;//this allows a valid private tag to be created without ensuring that the creator tag is added to the dataset

            //This loop reassigns the private creator block if that block already is in use, see Dicom standard 7.8.1.c:
            //"Encoders of Private Data Elements shall be able to dynamically assign private data to any available(unreserved) block(s) within the Private group,
            //and specify this assignment through the blocks corresponding Private Creator Data Element(s). Decoders of Private Data shall be able to accept
            //reserved blocks with a given Private Creator identification code at any position within the Private group specified by the blocks corresponding
            //Private Creator Data Element."

           ushort group = 0x0010;
            for (; group <= 0x00ff; group++)
            {
                var creator = new DicomTag(tag.Group, group);//no private creator assigned to the creator tag
                if (!Contains(creator))
                {
                    if (!createTag) continue;

                    Add(new DicomLongString(creator, tag.PrivateCreator.Creator));//writes the creator tag to the dataset

                    //(observation:  (tag.Element & 0xff) seems unnecessary since there is a check that tag.Element is not greater than 0xff above)
                    return new DicomTag(tag.Group, (ushort)((group << 8) + (tag.Element & 0xff)), tag.PrivateCreator);
                }
                var value = TryGetSingleValue(creator, out string tmpValue) ? tmpValue : string.Empty;
                if (tag.PrivateCreator.Creator == value) return new DicomTag(tag.Group, (ushort)((group << 8) + (tag.Element & 0xff)), tag.PrivateCreator);
            }
            return null;
        }

        /// <summary>
        /// Add a collection of DICOM items to the dataset.
        /// </summary>
        /// <param name="items">Collection of DICOM items to add.</param>
        /// <returns>The dataset instance.</returns>
        /// <exception cref="ArgumentException">If tag of added item already exists in dataset.</exception>
        public DicomDataset Add(params DicomItem[] items)
        {
            return DoAdd(items, false);
        }

        /// <summary>
        /// Add a collection of DICOM items to the dataset.
        /// </summary>
        /// <param name="items">Collection of DICOM items to add.</param>
        /// <returns>The dataset instance.</returns>
        /// <exception cref="ArgumentException">If tag of added item already exists in dataset.</exception>
        public DicomDataset Add(IEnumerable<DicomItem> items)
        {
            return DoAdd(items, false);
        }

        /// <summary>
        /// Add single DICOM item given by <paramref name="tag"/> and <paramref name="values"/>.
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <returns>The dataset instance.</returns>
        /// <exception cref="ArgumentException">If tag already exists in dataset.</exception>
        public DicomDataset Add<T>(DicomTag tag, params T[] values)
        {
            return DoAdd(tag, values, false);
        }

        /// <summary>
        /// Add single DICOM item given by <paramref name="vr"/>, <paramref name="tag"/> and <paramref name="values"/>.
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="vr">DICOM vr of the added item. Use when setting a private element.</param>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <remarks>No validation is performed on the <paramref name="vr"/> matching the element <paramref name="tag"/>
        /// This method is useful when adding a private tag and need to explicitly set the VR of the created element.
        /// </remarks>
        /// <returns>The dataset instance.</returns>
        /// <exception cref="ArgumentException">If tag already exists in dataset.</exception>
        public DicomDataset Add<T>(DicomVR vr, DicomTag tag, params T[] values)
        {
            return DoAdd(vr, tag, values, false);
        }

        /// <summary>
        /// Add a collection of DICOM items to the dataset. Update existing items.
        /// </summary>
        /// <param name="items">Collection of DICOM items to add.</param>
        /// <returns>The dataset instance.</returns>
        public DicomDataset AddOrUpdate(params DicomItem[] items)
        {
            return DoAdd(items, true);
        }

        /// <summary>
        /// Add a collection of DICOM items to the dataset. Update existing items.
        /// </summary>
        /// <param name="items">Collection of DICOM items to add.</param>
        /// <returns>The dataset instance.</returns>
        public DicomDataset AddOrUpdate(IEnumerable<DicomItem> items)
        {
            return DoAdd(items, true);
        }

        /// <summary>
        /// Add or update a single DICOM item given by <paramref name="tag"/> and <paramref name="values"/>. 
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <returns>The dataset instance.</returns>
        public DicomDataset AddOrUpdate<T>(DicomTag tag, params T[] values)
        {
            return DoAdd(tag, values, true);
        }

        /// <summary>
        /// Add or update a single DICOM item given by <paramref name="vr"/>, <paramref name="tag"/> and <paramref name="values"/>. 
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="vr">DICOM vr of the added item. Use when setting a private element.</param>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <remarks>No validation is performed on the <paramref name="vr"/> matching the element <paramref name="tag"/>
        /// This method is useful when adding a private tag and need to explicitly set the VR of the created element.
        /// </remarks>
        /// <returns>The dataset instance.</returns>
        public DicomDataset AddOrUpdate<T>(DicomVR vr, DicomTag tag, params T[] values)
        {
            return DoAdd(vr, tag, values, true);
        }

        /// <summary>
        /// Add or update the image pixel data element in the dataset
        /// </summary>
        /// <param name="vr">DICOM vr of the image pixel. For a PixelData element this value should be either DicomVR.OB or DicomVR.OW DICOM VR.</param>
        /// <param name="pixelData">An <see cref="IByteBuffer"/> that holds the image pixel data </param>
        /// <param name="transferSyntax">A DicomTransferSyntax object of the <paramref name="pixelData"/> parameter. 
        /// If parameter is not provided (null), then the default TransferSyntax "ExplicitVRLittleEndian" will be applied to the dataset</param>
        /// <remarks>Use this method whenever you are attaching an external image pixel data to the dataset and provide the proper TransferSyntax</remarks>
        /// <returns>The dataset instance.</returns>
        public DicomDataset AddOrUpdatePixelData(DicomVR vr, IByteBuffer pixelData, DicomTransferSyntax transferSyntax = null)
        {
            this.AddOrUpdate(vr, DicomTag.PixelData, pixelData);

            if (null != transferSyntax)
            {
                InternalTransferSyntax = transferSyntax;
            }

            return this;
        }

        /// <summary>
        /// Checks the DICOM dataset to determine if the dataset already contains an item with the specified tag.
        /// </summary>
        /// <param name="tag">DICOM tag to test</param>
        /// <returns><c>True</c> if a DICOM item with the specified tag already exists.</returns>
        public bool Contains(DicomTag tag)
        {
            if (tag.IsPrivate)
            {
                var privateTag = GetPrivateTag(tag, false);
                return (privateTag != null) && _items.Any(kv => kv.Key.Equals(privateTag));
            }
            return _items.ContainsKey(tag);
        }

        /// <summary>
        /// Removes items for specified tags.
        /// </summary>
        /// <param name="tags">DICOM tags to remove</param>
        /// <returns>Current Dataset</returns>
        public DicomDataset Remove(params DicomTag[] tags)
        {
            foreach (DicomTag tag in tags) _items.Remove(tag);
            return this;
        }

        /// <summary>
        /// Removes items where the selector function returns true.
        /// </summary>
        /// <param name="selector">Selector function</param>
        /// <returns>Current Dataset</returns>
        public DicomDataset Remove(Func<DicomItem, bool> selector)
        {
            foreach (DicomItem item in _items.Values.Where(selector).ToArray()) _items.Remove(item.Tag);
            return this;
        }

        /// <summary>
        /// Removes all items from the dataset.
        /// </summary>
        /// <returns>Current Dataset</returns>
        public DicomDataset Clear()
        {
            _items.Clear();
            return this;
        }

        public void Close(bool suppressErrors = false)
        {
            var logger = Dicom.Log.LogManager.GetLogger("DicomDataset");
            foreach (DicomItem item in this)
            {
                if (item == null) continue;
                try
                {
                    if (item is DicomSequence)
                    {
                        DicomSequence seq = (DicomSequence)item;
                        foreach (DicomDataset sqSet in seq.Items)
                        {
                            sqSet?.Close();
                        }
                    }
                    else if (item is DicomElement)
                    {
                        DicomElement ele = item as DicomElement;
                        ele.Buffer.Close();
                    }
                    else if (item is DicomFragmentSequence)
                    {
                        DicomFragmentSequence dfs = item as DicomFragmentSequence;
                        foreach (var buf in dfs.Fragments)
                        {
                            buf?.Close();
                        }
                    }
                }
                catch (Exception x)
                {
                    logger.Warn("Exception closing dataset: " + x.Message);
                    if (!suppressErrors) throw;
                }
            }
            Clear();
        }

        /// <summary>
        /// Copies all items to the destination dataset.
        /// </summary>
        /// <param name="destination">Destination Dataset</param>
        /// <returns>Current Dataset</returns>
        public DicomDataset CopyTo(DicomDataset destination)
        {
            if (destination != null) destination.AddOrUpdate(this);
            return this;
        }

        /// <summary>
        /// Copies tags to the destination dataset.
        /// </summary>
        /// <param name="destination">Destination Dataset</param>
        /// <param name="tags">Tags to copy</param>
        /// <returns>Current Dataset</returns>
        public DicomDataset CopyTo(DicomDataset destination, params DicomTag[] tags)
        {
            if (destination != null)
            {
                foreach (var tag in tags) destination.AddOrUpdate(Get<DicomItem>(tag));
            }
            return this;
        }

        /// <summary>
        /// Copies tags matching mask to the destination dataset.
        /// </summary>
        /// <param name="destination">Destination Dataset</param>
        /// <param name="mask">Tags to copy</param>
        /// <returns>Current Dataset</returns>
        public DicomDataset CopyTo(DicomDataset destination, DicomMaskedTag mask)
        {
            destination?.AddOrUpdate(_items.Values.Where(x => mask.IsMatch(x.Tag)));
            return this;
        }

        /// <summary>
        /// Enumerates all DICOM items.
        /// </summary>
        /// <returns>Enumeration of DICOM items</returns>
        public IEnumerator<DicomItem> GetEnumerator()
        {
            return _items.Values.GetEnumerator();
        }

        /// <summary>
        /// Enumerates all DICOM items.
        /// </summary>
        /// <returns>Enumeration of DICOM items</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"DICOM Dataset [{_items.Count} items]";
        }

        /// <summary>
        /// Gets the item or element value of the specified <paramref name="tag"/>. 
        /// </summary>
        /// <typeparam name="T">Type of the return value.</typeparam>
        /// <param name="tag">Requested DICOM tag.</param>
        /// <param name="n">Item index (for multi-valued elements).</param>
        /// <param name="useDefault">Indicates whether to use default value (true) or throw (false) if <paramref name="tag"/> is not contained in dataset.</param>
        /// <param name="defaultValue">Default value to apply if <paramref name="tag"/> is not contained in dataset and <paramref name="useDefault"/> is true.</param>
        /// <returns>Item or element value corresponding to <paramref name="tag"/>.</returns>
        private T Get<T>(DicomTag tag, int n, bool useDefault, T defaultValue)
        {
            DicomItem item = null;
            if (!_items.TryGetValue(tag, out item))
            {
                if (useDefault) return defaultValue;
                throw new DicomDataException("Tag: {0} not found in dataset", tag);
            }

            if (typeof(T) == typeof(DicomItem)) return (T)(object)item;

            if (typeof(T).GetTypeInfo().IsSubclassOf(typeof(DicomItem))) return (T)(object)item;

            if (typeof(T) == typeof(DicomVR)) return (T)(object)item.ValueRepresentation;

            if (item.GetType().GetTypeInfo().IsSubclassOf(typeof(DicomElement)))
            {
                DicomElement element = (DicomElement)item;

                if (typeof(IByteBuffer).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo())) return (T)(object)element.Buffer;

                if (typeof(T) == typeof(byte[])) return (T)(object)element.Buffer.Data;

                if (!typeof(T).GetTypeInfo().IsArray && (n >= element.Count || element.Count == 0))
                {
                    if (useDefault) return defaultValue;
                    throw new DicomDataException("Element empty or index: {0} exceeds element count: {1}", n, element.Count);
                }

                try
                {
                    return element.Get<T>(n);
                }
                catch (InvalidCastException)
                {
                    try
                    {
                        //try coercing it to it's default VR
                        if (!tag.DictionaryEntry.ValueRepresentations.Contains(element.ValueRepresentation))
                        {
                            string tempVal = element.Get<string>(-1);
                            string[] eValues = tempVal.Split('\\');
                            DicomDataset temp = new DicomDataset();
                            temp.Add(item.Tag, eValues);
                            if (temp._items.TryGetValue(tag, out item))
                            {
                                element = (DicomElement)item;
                                return element.Get<T>(n);
                            }
                        }
                    }
                    catch { }
                    throw;//the original exception
                }
            }

            if (item.GetType() == typeof(DicomSequence))
            {
                if (typeof(T) == typeof(DicomCodeItem)) return (T)(object)new DicomCodeItem((DicomSequence)item);

                if (typeof(T) == typeof(DicomMeasuredValue)) return (T)(object)new DicomMeasuredValue((DicomSequence)item);

                if (typeof(T) == typeof(DicomReferencedSOP)) return (T)(object)new DicomReferencedSOP((DicomSequence)item);
            }

            throw new DicomDataException(
                "Unable to get a value type of {0} from DICOM item of type {1}",
                typeof(T),
                item.GetType());
        }

        /// <summary>
        /// Add a collection of DICOM items to the dataset.
        /// </summary>
        /// <param name="items">Collection of DICOM items to add.</param>
        /// <param name="allowUpdate">True if existing tag can be updated, false if method should throw when trying to add already existing tag.</param>
        /// <returns>The dataset instance.</returns>
        private DicomDataset DoAdd(IEnumerable<DicomItem> items, bool allowUpdate)
        {
            if (items != null)
            {
                if (allowUpdate)
                {
                    foreach (var item in items.Where(i => i != null))
                    {
                        //_items[item.Tag.IsPrivate ? GetPrivateTag(item.Tag) : item.Tag] = item;
                        var tag = item.Tag;
                        //ValidateTag(tag);
                        if (tag.IsPrivate)
                        {
                            tag = GetPrivateTag(tag);
                            item.Tag = tag;
                        }
                        if (ValidateItems) item.Validate();
                        _items[tag] = item;
                    }
                }
                else
                {
                    foreach (var item in items.Where(i => i != null))
                    {
                        //_items.Add(item.Tag.IsPrivate ? GetPrivateTag(item.Tag) : item.Tag, item);
                        var tag = item.Tag;
                        //ValidateTag(tag);
                        if (tag.IsPrivate)
                        {
                            tag = GetPrivateTag(tag);
                            item.Tag = tag;
                        }
                        if (ValidateItems) item.Validate();
                        _items.Add(tag, item);
                    }
                }
            }
            return this;
        }

        /// <summary>
        /// Add single DICOM item to the dataset.
        /// </summary>
        /// <param name="item">DICOM item to add.</param>
        /// <param name="allowUpdate">True if existing tag can be updated, false if method should throw when trying to add already existing tag.</param>
        /// <returns>The dataset instance.</returns>
        private DicomDataset DoAdd(DicomItem item, bool allowUpdate)
        {
            if (item != null)
            {
                var tag = item.Tag;
                //ValidateTag(tag);
                if (tag.IsPrivate)
                {
                    tag = GetPrivateTag(tag);
                    item.Tag = tag;
                }
                if (ValidateItems) item.Validate();
                if (allowUpdate)
                {
                    _items[tag] = item;
                }
                else
                {
                    _items.Add(tag, item);
                }
            }
            return this;
        }

        /// <summary>
        /// Add single DICOM item given by <paramref name="tag"/> and <paramref name="values"/>.
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <param name="allowUpdate">True if existing tag can be updated, false if method should throw when trying to add already existing tag.</param>
        /// <returns>The dataset instance.</returns>
        private DicomDataset DoAdd<T>(DicomTag tag, IList<T> values, bool allowUpdate)
        {
            var entry = DicomDictionary.Default[tag];
            if (entry == null)
                throw new DicomDataException(
                    "Tag {0} not found in DICOM dictionary. Only dictionary tags may be added implicitly to the dataset.",
                    tag);

            DicomVR vr = null;
            if (entry == DicomDictionary.UnknownTag && typeof(T) == typeof(string) && values != null)
            {
                //make sure we get a bit enough VR for the string
                long valLen = 0;
                for(int i=0; i< values.Count; i++)
                {
                    if (values[i] != null)
                    {
                        T curVal = values[i];
                        valLen += curVal.ToString().Length;
                        if (i > 0) valLen++;//accound for multiplicity separator
                    }
                }
                if(values.Count == 1)
                {
                    if (valLen < DicomVR.SH.MaximumLength)
                    {
                        vr = DicomVR.SH;
                    }
                    else if (valLen < DicomVR.LO.MaximumLength)
                    {
                        vr = DicomVR.LO;
                    }
                    else if (valLen < DicomVR.ST.MaximumLength)
                    {
                        vr = DicomVR.ST;
                    }
                }
                if (vr == null) {
                    if (valLen < DicomVR.LT.MaximumLength)
                    {
                        vr = DicomVR.LT;
                    }
                    else
                    {
                        vr = DicomVR.UT;
                    }
                }
            }
            else
            {
                if (values != null) vr = entry.ValueRepresentations.FirstOrDefault(x => x.ValueType == typeof(T));
                if (vr == null) vr = entry.ValueRepresentations.First();
            }

            return DoAdd(vr, tag, values, allowUpdate);
        }

        /// <summary>
        /// Add single DICOM item given by <paramref name="vr"/>, <paramref name="tag"/> and <paramref name="values"/>.
        /// </summary>
        /// <typeparam name="T">Type of added values.</typeparam>
        /// <param name="vr">DICOM vr of the added item. Use when setting a private element.</param>
        /// <param name="tag">DICOM tag of the added item.</param>
        /// <param name="values">Values of the added item.</param>
        /// <param name="allowUpdate">True if existing tag can be updated, false if method should throw when trying to add already existing tag.</param>
        /// <remarks>No validation is performed on the <paramref name="vr"/> matching the element <paramref name="tag"/>
        /// This method is useful when adding a private tag and need to explicitly set the VR of the created element.
        /// </remarks>
        /// <returns>The dataset instance.</returns>
        private DicomDataset DoAdd<T>(DicomVR vr, DicomTag tag, IList<T> values, bool allowUpdate)
        {
            if (vr == DicomVR.AE)
            {
                if (values == null) return DoAdd(new DicomApplicationEntity(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomApplicationEntity(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.AS)
            {
                if (values == null) return DoAdd(new DicomAgeString(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomAgeString(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.AT)
            {
                if (values == null) return DoAdd(new DicomAttributeTag(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(DicomTag)) return DoAdd(new DicomAttributeTag(tag, values.Cast<DicomTag>().ToArray()), allowUpdate);

                IEnumerable<DicomTag> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, DicomTag.Parse, out parsedValues))
                {
                    return DoAdd(new DicomAttributeTag(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.CS)
            {
                if (values == null) return DoAdd(new DicomCodeString(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomCodeString(tag, values.Cast<string>().ToArray()), allowUpdate);
                if (typeof(T).GetTypeInfo().IsEnum) return DoAdd(new DicomCodeString(tag, values.Select(x => x.ToString()).ToArray()), allowUpdate);
            }

            if (vr == DicomVR.DA)
            {
                if (values == null) return DoAdd(new DicomDate(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(DateTime)) return DoAdd(new DicomDate(tag, values.Cast<DateTime>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomDateRange))
                    return
                        DoAdd(new DicomDate(tag, values.Cast<DicomDateRange>().FirstOrDefault() ?? new DicomDateRange()), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomDate(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.DS)
            {
                if (values == null) return DoAdd(new DicomDecimalString(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(decimal)) return DoAdd(new DicomDecimalString(tag, values.Cast<decimal>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomDecimalString(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.DT)
            {
                if (values == null) return DoAdd(new DicomDateTime(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(DateTime)) return DoAdd(new DicomDateTime(tag, values.Cast<DateTime>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomDateRange))
                    return
                        DoAdd(
                            new DicomDateTime(
                                tag,
                                values.Cast<DicomDateRange>().FirstOrDefault() ?? new DicomDateRange()), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomDateTime(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.FD)
            {
                if (values == null) return DoAdd(new DicomFloatingPointDouble(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(double)) return DoAdd(new DicomFloatingPointDouble(tag, values.Cast<double>().ToArray()), allowUpdate);

                IEnumerable<double> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, double.Parse, out parsedValues))
                {
                    return DoAdd(new DicomFloatingPointDouble(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.FL)
            {
                if (values == null) return DoAdd(new DicomFloatingPointSingle(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(float)) return DoAdd(new DicomFloatingPointSingle(tag, values.Cast<float>().ToArray()), allowUpdate);

                IEnumerable<float> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, float.Parse, out parsedValues))
                {
                    return DoAdd(new DicomFloatingPointSingle(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.IS)
            {
                if (values == null) return DoAdd(new DicomIntegerString(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(int)) return DoAdd(new DicomIntegerString(tag, values.Cast<int>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomIntegerString(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.LO)
            {
                if (values == null) return DoAdd(new DicomLongString(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomLongString(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.LT)
            {
                if (values == null) return DoAdd(new DicomLongText(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomLongText(tag, values.Cast<string>().First()), allowUpdate);
            }

            if (vr == DicomVR.OB)
            {
                if (values == null) return DoAdd(new DicomOtherByte(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(byte)) return DoAdd(new DicomOtherByte(tag, values.Cast<byte>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomOtherByte(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.OD)
            {
                if (values == null) return DoAdd(new DicomOtherDouble(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(double)) return DoAdd(new DicomOtherDouble(tag, values.Cast<double>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomOtherDouble(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.OF)
            {
                if (values == null) return DoAdd(new DicomOtherFloat(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(float)) return DoAdd(new DicomOtherFloat(tag, values.Cast<float>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomOtherFloat(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.OL)
            {
                if (values == null) return DoAdd(new DicomOtherLong(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(uint)) return DoAdd(new DicomOtherLong(tag, values.Cast<uint>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomOtherLong(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.OW)
            {
                if (values == null) return DoAdd(new DicomOtherWord(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(ushort)) return DoAdd(new DicomOtherWord(tag, values.Cast<ushort>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomOtherWord(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.PN)
            {
                if (values == null) return DoAdd(new DicomPersonName(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomPersonName(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.SH)
            {
                if (values == null) return DoAdd(new DicomShortString(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomShortString(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.SL)
            {
                if (values == null) return DoAdd(new DicomSignedLong(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(int)) return DoAdd(new DicomSignedLong(tag, values.Cast<int>().ToArray()), allowUpdate);

                IEnumerable<int> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, int.Parse, out parsedValues))
                {
                    return DoAdd(new DicomSignedLong(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.SQ)
            {
                if (values == null) return DoAdd(new DicomSequence(tag), allowUpdate);
                if (typeof(T) == typeof(DicomContentItem)) return DoAdd(new DicomSequence(tag, values.Cast<DicomContentItem>().Select(x => x.Dataset).ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomDataset) || typeof(T) == typeof(DicomCodeItem)
                    || typeof(T) == typeof(DicomMeasuredValue) || typeof(T) == typeof(DicomReferencedSOP)) return DoAdd(new DicomSequence(tag, values.Cast<DicomDataset>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.SS)
            {
                if (values == null) return DoAdd(new DicomSignedShort(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(short)) return DoAdd(new DicomSignedShort(tag, values.Cast<short>().ToArray()), allowUpdate);

                IEnumerable<short> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, short.Parse, out parsedValues))
                {
                    return DoAdd(new DicomSignedShort(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.ST)
            {
                if (values == null) return DoAdd(new DicomShortText(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomShortText(tag, values.Cast<string>().First()), allowUpdate);
            }

            if (vr == DicomVR.TM)
            {
                if (values == null) return DoAdd(new DicomTime(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(DateTime)) return DoAdd(new DicomTime(tag, values.Cast<DateTime>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomDateRange))
                    return
                        DoAdd(new DicomTime(tag, values.Cast<DicomDateRange>().FirstOrDefault() ?? new DicomDateRange()), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomTime(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.UC)
            {
                if (values == null) return DoAdd(new DicomUnlimitedCharacters(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomUnlimitedCharacters(tag, values.Cast<string>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.UI)
            {
                if (values == null) return DoAdd(new DicomUniqueIdentifier(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomUniqueIdentifier(tag, values.Cast<string>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomUID)) return DoAdd(new DicomUniqueIdentifier(tag, values.Cast<DicomUID>().ToArray()), allowUpdate);
                if (typeof(T) == typeof(DicomTransferSyntax)) return DoAdd(new DicomUniqueIdentifier(tag, values.Cast<DicomTransferSyntax>().ToArray()), allowUpdate);
            }

            if (vr == DicomVR.UL)
            {
                if (values == null) return DoAdd(new DicomUnsignedLong(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(uint)) return DoAdd(new DicomUnsignedLong(tag, values.Cast<uint>().ToArray()), allowUpdate);

                IEnumerable<uint> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, uint.Parse, out parsedValues))
                {
                    return DoAdd(new DicomUnsignedLong(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.UN)
            {
                if (values == null) return DoAdd(new DicomUnknown(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(byte)) return DoAdd(new DicomUnknown(tag, values.Cast<byte>().ToArray()), allowUpdate);

                if (typeof(T) == typeof(IByteBuffer) && values.Count == 1)
                {
                    return DoAdd(new DicomUnknown(tag, (IByteBuffer)values[0]), allowUpdate);
                }
            }

            if (vr == DicomVR.UR)
            {
                if (values == null) return DoAdd(new DicomUniversalResource(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomUniversalResource(tag, values.Cast<string>().First()), allowUpdate);
            }

            if (vr == DicomVR.US)
            {
                if (values == null) return DoAdd(new DicomUnsignedShort(tag, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(ushort)) return DoAdd(new DicomUnsignedShort(tag, values.Cast<ushort>().ToArray()), allowUpdate);

                IEnumerable<ushort> parsedValues;
                if (ParseVrValueFromString(values, tag.DictionaryEntry.ValueMultiplicity, ushort.Parse, out parsedValues))
                {
                    return DoAdd(new DicomUnsignedShort(tag, parsedValues.ToArray()), allowUpdate);
                }
            }

            if (vr == DicomVR.UT)
            {
                if (values == null) return DoAdd(new DicomUnlimitedText(tag, DicomEncoding.Default, EmptyBuffer.Value), allowUpdate);
                if (typeof(T) == typeof(string)) return DoAdd(new DicomUnlimitedText(tag, values.Cast<string>().First()), allowUpdate);
            }

            throw new InvalidOperationException(
                $"Unable to create DICOM element of type {vr.Code} with values of type {typeof(T)}");
        }

        private static bool ParseVrValueFromString<T, TOut>(
            IEnumerable<T> values,
            DicomVM valueMultiplicity,
            Func<string, TOut> parser,
            out IEnumerable<TOut> parsedValues)
        {
            parsedValues = null;

            if (typeof(T) == typeof(string))
            {
                var stringValues = values.Cast<string>().ToArray();

                if (valueMultiplicity.Maximum > 1 && stringValues.Length == 1)
                {
                    stringValues = stringValues[0].Split('\\');
                }

                parsedValues = stringValues.Where(n => !string.IsNullOrEmpty(n?.Trim())).Select(parser);

                return true;
            }

            return false;
        }

        #endregion
    }
}
