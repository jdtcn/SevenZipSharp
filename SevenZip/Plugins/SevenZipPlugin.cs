using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SevenZip.Plugins
{
    internal class SevenZipPlugin : IDisposable
    {
        private IntPtr _handle;
        private readonly bool _multipleFormatsSupport;

        private SevenZipPlugin(IntPtr dllHandle)
        {
            _handle = dllHandle;
            _multipleFormatsSupport = NativeMethods.GetProcAddress(dllHandle, "GetHandlerProperty2") != IntPtr.Zero;
        }

        public static bool TryLoadPlugin(string pluginFilePath, out SevenZipPlugin plugin)
        {
            plugin = null;

            var handle = NativeMethods.LoadLibrary(pluginFilePath);
            if (handle == IntPtr.Zero)
                return false;

            if (NativeMethods.GetProcAddress(handle, "GetHandlerProperty") == IntPtr.Zero &&
                NativeMethods.GetProcAddress(handle, "GetHandlerProperty2") == IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(handle);
                return false;
            }

            plugin = new SevenZipPlugin(handle);
            return true;
        }

        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        private void DisposeInternal()
        {
            if (_handle == IntPtr.Zero)
                return;

            NativeMethods.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
        }

        // Should be never called
        ~SevenZipPlugin()
        {
            DisposeInternal();
        }

        public IEnumerable<SevenZipPluginFormat> GetFormats()
        {
            AssertDisposed();

            for (uint i = 0; i < GetNumberOfFormats(); i++)
            {
                var name = NativeMethods.SafeCast(GetProperty(i, PluginPropertyId.Name), string.Empty);
                var classId = new Guid(StringByteToByteArray(GetProperty(i, PluginPropertyId.ClassId).Value));
                var ext = NativeMethods.SafeCast(GetProperty(i, PluginPropertyId.Extension), string.Empty);
                var addExt = NativeMethods.SafeCast(GetProperty(i, PluginPropertyId.AddExtension), string.Empty);
                var signature = BitConverter.ToString(StringByteToByteArray(GetProperty(i, PluginPropertyId.Signature).Value));
                var signatures = ParseSignatures(NativeMethods.SafeCast(GetProperty(i, PluginPropertyId.MultiSignature), new object[0]));
                var signatureOffset = NativeMethods.SafeCast(GetProperty(i, PluginPropertyId.SignatureOffset), (uint)0);

                yield return new SevenZipPluginFormat(name, classId, ext, addExt, signature, signatures, signatureOffset, _handle);
            }
        }

        private PropVariant GetProperty(uint index, PluginPropertyId propId)
        {
            return _multipleFormatsSupport ? GetHandlerProperty2(index, propId) : GetHandlerProperty(propId);
        }

        private uint GetNumberOfFormats()
        {
            AssertHResult(GetFunction<GetNumberOfFormatsDelegate>("GetNumberOfFormats")(out var numFormats));
            return numFormats;
        }

        private PropVariant GetHandlerProperty(PluginPropertyId propId)
        {
            AssertHResult(GetFunction<GetHandlerPropertyDelegate>("GetHandlerProperty")(propId, out var value));
            return value;
        }

        private PropVariant GetHandlerProperty2(uint index, PluginPropertyId propId)
        {
            AssertHResult(GetFunction<GetHandlerProperty2Delegate>("GetHandlerProperty2")(index, propId, out var value));
            return value;
        }

        #region Helpers

        private void AssertDisposed()
        {
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(SevenZipPlugin));
        }

        private static string[] ParseSignatures(object[] signatures)
        {
            var values = new string[signatures.Length];

            for (var i = 0; i < signatures.Length; i++)
            {
                var sigHandle = GCHandle.Alloc(signatures[i], GCHandleType.Pinned);
                try
                {
                    var bytes = StringByteToByteArray(sigHandle.AddrOfPinnedObject());
                    values[i] = BitConverter.ToString(bytes);
                }
                finally
                {
                    sigHandle.Free();
                }
            }

            return values;
        }

        private static byte[] StringByteToByteArray(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return new byte[0];

            var length = NativeMethods.SysStringByteLen(ptr);
            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);

            return bytes;
        }

        private T GetFunction<T>(string name) where T : class
        {
            return Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_handle, name), typeof(T)) as T;
        }

        private static void AssertHResult(int hResult)
        {
            if (hResult != 0)
                throw new Win32Exception(hResult);
        }

        #endregion DLL functions

        #region Nested types

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetNumberOfFormatsDelegate(out uint numFormats);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetHandlerPropertyDelegate(PluginPropertyId propId, out PropVariant value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetHandlerProperty2Delegate(uint index, PluginPropertyId propId, out PropVariant value);

        private enum PluginPropertyId : uint
        {
            Name = 0,            // VT_BSTR
            ClassId = 1,         // binary GUID in VT_BSTR
            Extension = 2,       // VT_BSTR
            AddExtension = 3,    // VT_BSTR
            //Update = 4,        // VT_BOOL
            //KeepName = 5,      // VT_BOOL
            Signature = 6,       // binary in VT_BSTR
            MultiSignature = 7,  // binary in VT_BSTR
            SignatureOffset = 8, // VT_UI4
            //AltStreams = 9,    // VT_BOOL
            //NtSecure = 10,     // VT_BOOL
            //Flags = 11         // VT_UI4
            //Version = 12       // VT_UI4 ((VER_MAJOR << 8) | VER_MINOR)
        };

        #endregion DLL delgates
    }
}