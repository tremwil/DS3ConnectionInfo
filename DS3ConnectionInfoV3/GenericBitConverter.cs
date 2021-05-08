using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// A set of method for converting generic objects to bytes and vice-versa.
    /// </summary>
    public static class GenericBitConverter
    {
        /// <summary>
        /// Get the size, in bytes, of a type in a way that supports enums.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns></returns>
        public static int GetTypeSize(Type t)
        {
            if (t.IsEnum)
            {
                return Marshal.SizeOf(Enum.GetUnderlyingType(t));
            }
            if (t.IsValueType)
            {
                return Marshal.SizeOf(t);
            }
            throw new ArgumentException("Cannot determine size of a non value-type object, got " + t.FullName);
        }

        /// <summary>
        /// Convert bytes to a specific struct.
        /// </summary>
        /// <typeparam name="T">The struct to convert to.</typeparam>
        /// <param name="data">The data to convert.</param>
        /// <param name="safe">If true, the method will throw an error if type's size does not match the data.</param>
        /// <returns></returns>
        public static T ToStruct<T>(byte[] data, bool safe) where T : struct
        {
            Type t = typeof(T);
            if (t.IsEnum)
            {
                t = Enum.GetUnderlyingType(t);
            }

            if (safe)
                if (Marshal.SizeOf(t) != data.Length)
                    throw new ArgumentException("Data does not match type size", "data");

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            T obj;

            try
            {
                obj = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), t);
            }
            finally
            {
                handle.Free();
            }

            return obj;
        }

        /// <summary>
        /// Convert bytes to a sequential or explicit struct.
        /// </summary>
        /// <typeparam name="T">The struct to convert to.</typeparam>
        /// <param name="data">The data to convert.</param>
        /// <returns></returns>
        public static T ToStruct<T>(byte[] data) where T : struct
        {
            return ToStruct<T>(data, false);
        }

        /// <summary>
        /// Convert a value-type to bytes.
        /// </summary>
        /// <typeparam name="T">The struct to convert from.</typeparam>
        /// <param name="value">The value to convert to bytes.</param>
        /// <returns></returns>
        public static byte[] ToBytes<T>(T value) where T : struct
        {
            object fixedValue = value;
            Type t = typeof(T);

            if (t.IsEnum)
            {
                t = Enum.GetUnderlyingType(t);
                fixedValue = Convert.ChangeType(fixedValue, t);
            }

            byte[] buffer = new byte[Marshal.SizeOf(t)];
            IntPtr handle = Marshal.AllocHGlobal(buffer.Length);

            try
            {
                Marshal.StructureToPtr(fixedValue, handle, false);
                Marshal.Copy(handle, buffer, 0, buffer.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(handle);
            }

            return buffer;
        }
    }
}