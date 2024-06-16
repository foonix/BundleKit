using AssetsTools.NET;
using System.IO;

namespace BundleKit.Assets.Replacers
{
    /// <summary>
    /// Serialize the provided AssetBaseValueField reference at write time. (Closure)
    ///
    /// Use this to set up serialization on an aset that is still actively being manipulated,
    /// or to defer generating the serialized byte[] until it's actually needed (and then can be GC'd)
    /// </summary>
    public class DeferredBaseFieldSerializer : IContentReplacer
    {
        private AssetTypeValueField baseField;
        readonly private bool discardAfterWrite;

        /// <summary>
        /// Serialize baseField at write time, discarding the reference if discardAfterWrite is true.
        ///
        /// Note that discarding the reference means the writer will only work once.
        /// </summary>
        /// <param name="baseField">Reference to the asset's base field</param>
        /// <param name="discardAfterWrite">If true, the reference will be discarded after write so that it can be garbage collected.</param>
        public DeferredBaseFieldSerializer(AssetTypeValueField baseField, bool discardAfterWrite = true)
        {
            this.baseField = baseField;
            this.discardAfterWrite = discardAfterWrite;
        }

        public Stream GetPreviewStream() =>
            throw new System.NotImplementedException();

        public ContentReplacerType GetReplacerType() => ContentReplacerType.AddOrModify;

        public bool HasPreview() => false;

        public void Write(AssetsFileWriter writer)
        {
            writer.BaseStream.Write(baseField.WriteToByteArray(writer.BigEndian));
            if (discardAfterWrite)
            {
                baseField = null;
            }
        }
    }
}