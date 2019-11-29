using System;

namespace SevenZip.Plugins
{
    internal class SevenZipPluginFormat
    {
        private readonly string _signature;
        private readonly string[] _signatures;
        private readonly uint _signatureOffset;

        public string Name { get; }
        public Guid ClassId { get; }
        public string Extension { get; }
        public string AddExtension { get; }
        public IntPtr Handle { get; }

        public SevenZipPluginFormat(string name, Guid classId, string extension, string addExtension, string signature, string[] signatures, uint signatureOffset, IntPtr handle)
        {
            Name = name;
            ClassId = classId;
            Extension = extension;
            AddExtension = addExtension;
            _signature = signature;
            _signatures = signatures;
            _signatureOffset = signatureOffset;
            Handle = handle;

            if (_signatureOffset > 0)
                throw new NotSupportedException("Plugins with signature offsets are not supported"); // TODO: SignatureOffset
        }

        public bool MatchSignature(string actualSignature)
        {
            if (_signature != null)
                if (actualSignature.StartsWith(_signature, StringComparison.OrdinalIgnoreCase))
                    return true;

            if (_signatures != null)
                foreach (var sig in _signatures)
                    if (actualSignature.StartsWith(sig, StringComparison.OrdinalIgnoreCase))
                        return true;

            return false;
        }
    }
}