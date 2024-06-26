﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom
{
    using Dicom.IO.Buffer;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// <see cref="DicomDataset"/> extension methods.
    /// </summary>
    public static class DicomDatasetExtensions
    {
        /// <summary>
        /// Clone a dataset.
        /// </summary>
        /// <param name="dataset">Dataset to be cloned.</param>
        /// <returns>Clone of dataset.</returns>
        public static DicomDataset Clone(this DicomDataset dataset)
        {
            var ds = new DicomDataset(dataset);
            ds.InternalTransferSyntax = dataset.InternalTransferSyntax;
            return ds;
        }

        /// <summary>
        /// Get a composite <see cref="DateTime"/> instance based on <paramref name="date"/> and <paramref name="time"/> values.
        /// </summary>
        /// <param name="dataset">Dataset from which data should be retrieved.</param>
        /// <param name="date">Tag associated with date value.</param>
        /// <param name="time">Tag associated with time value.</param>
        /// <returns>Composite <see cref="DateTime"/>.</returns>
        public static DateTime GetDateTime(this DicomDataset dataset, DicomTag date, DicomTag time)
        {
            var dd = dataset.Contains(date) ? dataset.Get<DicomDate>(date) : null;
            var dt = dataset.Contains(time) ? dataset.Get<DicomTime>(time) : null;

            var da = dd != null && dd.Count > 0 ? dd.Get<DateTime>(0) : DateTime.MinValue;
            var tm = dt != null && dt.Count > 0 ? dt.Get<DateTime>(0) : DateTime.MinValue;

            return new DateTime(da.Year, da.Month, da.Day, tm.Hour, tm.Minute, tm.Second);
        }

        /// <summary>
        /// Enumerates DICOM items matching mask.
        /// </summary>
        /// <param name="dataset">Dataset from which masked items should be retrieved.</param>
        /// <param name="mask">Requested mask.</param>
        /// <returns>Enumeration of masked DICOM items.</returns>
        public static IEnumerable<DicomItem> EnumerateMasked(this DicomDataset dataset, DicomMaskedTag mask)
        {
            return dataset.Where(x => mask.IsMatch(x.Tag));
        }

        /// <summary>
        /// Enumerates DICOM items for specified group.
        /// </summary>
        /// <param name="dataset">Dataset from which group items should be retrieved.</param>
        /// <param name="group">Requested group.</param>
        /// <returns>Enumeration of DICOM items for specified <paramref name="group"/>.</returns>
        public static IEnumerable<DicomItem> EnumerateGroup(this DicomDataset dataset, ushort group)
        {
            return dataset.Where(x => x.Tag.Group == group && x.Tag.Element != 0x0000);
        }

        /// <summary>
        /// Turns off validation on the passed DicomDataset and returns this Dataset
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public static DicomDataset NotValidated(this DicomDataset dataset)
        {
            dataset.ValidateItems = false;
            return dataset;
        }

        /// <summary>
        /// Turns on validation on the passed DicomDataset and returns this Dataset
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public static DicomDataset Validated(this DicomDataset dataset)
        {
            dataset.ValidateItems = true;
            return dataset;
        }

        /// <summary>
        /// throws exception containing all invalid tags
        /// </summary>
        public static bool Validate(this DicomDataset dds)
        {
            string result = "";
            int errCount = 0;
            int indentSize = 4;
            DicomDictionary dict = DicomDictionary.Default;
            foreach (var item in dds)
            {
                if (item.Tag != null)
                {
                    if (item is DicomSequence)
                    {
                        DicomSequence seq = (DicomSequence)item;
                        if (seq.Items.Count > 0)
                        {
                            foreach (DicomDataset sdat in seq.Items)
                            {
                                try
                                {
                                    sdat.Validate();
                                }
                                catch (DicomValidationException sdvx)
                                {
                                    string sErrs = sdvx.Message;
                                    errCount += sErrs.Split('\n').Length;
                                    if (result.Length > 0) result += "\n";
                                    result += sdvx.Message;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (item.Tag == DicomTag.PixelData)
                        {
                            //not validated
                        }
                        else
                        {
                            try
                            {
                                item.Validate();
                            }
                            catch (DicomValidationException dvx)
                            {
                                errCount++;
                                if (result.Length > 0) result += "\n";
                                result += dvx.Message;
                            }
                        }
                    }
                }
            }
            if(errCount > 0)
            {
                result = "Dataset has " + errCount + " validation errors\n" + result;
                throw new DicomValidationException(null, null, result);
            }
            return true;
        }

        public static IList<IByteBuffer> GetContainedBuffers(this DicomDataset dataset)
        {
            List<IByteBuffer> buffers = new List<IByteBuffer>();
            foreach (DicomItem item in dataset)
            {
                if (item is DicomSequence)
                {
                    DicomSequence seq = (DicomSequence)item;
                    foreach (DicomDataset sqSet in seq.Items)
                    {
                        buffers.AddRange(sqSet.GetContainedBuffers());
                    }
                }
                else if (item is DicomElement)
                {
                    DicomElement ele = item as DicomElement;
                    buffers.AddRange(GetRootBuffers(ele.Buffer));
                }
                else if (item is DicomFragmentSequence)
                {
                    DicomFragmentSequence dfs = item as DicomFragmentSequence;
                    foreach (var buf in dfs.Fragments)
                    {
                        buffers.AddRange(GetRootBuffers(buf));
                    }
                }
            }
            return buffers;
        }

        public static IByteBuffer[] GetRootBuffers(IByteBuffer curBuffer)
        {
            List<IByteBuffer> buffers = new List<IByteBuffer>();
            if (curBuffer is CompositeByteBuffer)
            {
                CompositeByteBuffer cbuf = (CompositeByteBuffer)curBuffer;
                foreach (var buf in cbuf.Buffers)
                {
                    buffers.AddRange(GetRootBuffers(buf));
                }
            }
            else if (curBuffer is RangeByteBuffer)
            {
                buffers.AddRange(GetRootBuffers(((RangeByteBuffer)curBuffer).Internal));
            }
            else
            {
                buffers.Add(curBuffer);
            }
            return buffers.ToArray();
        }


    }
}
