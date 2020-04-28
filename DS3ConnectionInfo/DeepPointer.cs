using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// Represents a system of muliple nested pointers.
    /// </summary>
    public class DeepPointerBase : IDisposable
    {
        /// <summary>
        /// The level of the pointer (the amount of steps to the value).
        /// </summary>
        public int Level => Offsets.Length + 1;

        /// <summary>
        /// The static address to start reading from.
        /// </summary>
        public long StaticAddress { get; }
        /// <summary>
        /// The offsets between each read in memory.
        /// </summary>
        public int[] Offsets { get; }

        /// <summary>
        /// The handle of the process to read from.
        /// </summary>
        public IntPtr ProcHandle { get; }
        /// <summary>
        /// The base address of a specific module in the process.
        /// </summary>
        public long ModuleBaseAddress { get; }

        /// <summary>
        /// Create a DeepPointer from an already known handle and module address.
        /// </summary>
        /// <param name="pHandle">The handle of the process to read from.</param>
        /// <param name="mbAddress">The base address of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerBase(IntPtr pHandle, long mbAddress, long sAddress, params int[] offsets)
        {
            ProcHandle = pHandle;
            ModuleBaseAddress = mbAddress;
            StaticAddress = sAddress;
            Offsets = offsets;
        }

        /// <summary>
        /// Create a DeepPointer from an already known handle and module name.
        /// <para></para> Note : for this function to work properly, the handle
        /// must have the QUERY_LIMITED_INFORMATION flag.
        /// </summary>
        /// <param name="pHandle">The handle of the process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerBase(IntPtr pHandle, string mname, long sAddress, params int[] offsets)
        {
            // Get the PID to get the process, allowing us to get the module address
            int pid = WinAPI.GetProcessId(pHandle);
            var proc = Process.GetProcessById(pid);

            ProcHandle = pHandle;
            ModuleBaseAddress = MemoryManager.GetModuleAddress(proc, mname);
            StaticAddress = sAddress;
            Offsets = offsets;
        }

        /// <summary>
        /// Create a DeepPointer from a process and module name.
        /// </summary>
        /// <param name="proc">The process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerBase(Process proc, string mname, long sAddress, params int[] offsets)
        {
            ProcHandle = WinAPI.OpenProcess(ProcessAccessFlags.VmRead, false, proc.Id);
            ModuleBaseAddress = MemoryManager.GetModuleAddress(proc, mname);
            StaticAddress = sAddress;
            Offsets = offsets;
        }

        /// <summary>
        /// Create a DeepPointer from a process name and module name.
        /// </summary>
        /// <param name="pname">The name of the process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerBase(string pname, string mname, long sAddress, params int[] offsets)
        {
            // Assuming only one process of name pname
            var proc = Process.GetProcessesByName(pname)[0];

            ProcHandle = WinAPI.OpenProcess(ProcessAccessFlags.VmRead, false, proc.Id);
            ModuleBaseAddress = MemoryManager.GetModuleAddress(proc, mname);
            StaticAddress = sAddress;
            Offsets = offsets;
        }

        /// <summary>
        /// A direct address to the value. Do not store this address to reliably
        /// get data since it can change at any time.
        /// </summary>
        /// <returns>The current address to the data, otherwise 0</returns>
        public long GetDynamicAddress()
        {
            try
            {
                // Add the first address (the static address) to our base address
                long cadress = ModuleBaseAddress + StaticAddress;

                // Iteratively update the current address by moving up the pointers chain
                foreach (long offset in Offsets)
                    cadress = MemoryManager.ReadInt64(ProcHandle, cadress) + offset;

                return cadress;
            }
            catch (UnauthorizedAccessException)
            {
                return 0; // Pointer is probably changing addresses
            }
        }

        /// <summary>
        /// Get the bytes this object points to, if possible.
        /// </summary>
        /// <param name="count">The amount of bytes to read.</param>
        /// <returns></returns>
        public byte[] GetBytes(int count)
        {
            long dyn = GetDynamicAddress();
            if (dyn == 0) return null; // Pointer has been resetted

            // Read from the memory at the dynamic address and return bytes
            return MemoryManager.ReadByteArray(ProcHandle, dyn, count);
        }

        /// <summary>
        /// Set the bytes this object points to, if possible.
        /// </summary>
        /// <param name="data">The bytes to write.</param>
        /// <returns>A bool indicating the success of this operation</returns>
        public bool SetBytes(byte[] data)
        {
            long dyn = GetDynamicAddress();
            if (dyn == 0) return false; // Pointer has been resetted

            // Write to the memory at the dynamic address
            MemoryManager.WriteByteArray(ProcHandle, dyn, data);
            return true;
        }

        private bool disposed = true;

        /// <summary>
        /// Free unmanaged resources used by this object.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Binding CloseHandle to Dispose() allows it to properly
            // close the handle when the object is garbage collected.
            if (!disposed && disposing)
                WinAPI.CloseHandle(ProcHandle);

            disposed = true;
        }

        /// <summary>
        /// Free unmanaged resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Represents a system of muliple nested pointers to a specific type.
    /// <para></para>NOTE: T must be a struct with a sequential or
    /// explicit layout or an unmanaged type.
    /// </summary>
    public class DeepPointer<T> : DeepPointerBase where T : struct
    {
        /// <summary>
        /// The size of the return type of this pointer, in bytes.
        /// </summary>
        public int TargetSize { get; }

        /// <summary>
        /// Create a DeepPointer from an already known handle and module address.
        /// </summary>
        /// <param name="pHandle">The handle of the process to read from.</param>
        /// <param name="mbAddress">The base address of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointer(IntPtr pHandle, long mbAddress, long sAddress, params int[] offsets)
        : base(pHandle, mbAddress, sAddress, offsets)
        {
            TargetSize = GenericBitConverter.GetTypeSize(typeof(T));
        }

        /// <summary>
        /// Create a DeepPointer from an already known handle and module name.
        /// <para></para> Note : for this function to work properly, the handle
        /// must have the QUERY_LIMITED_INFORMATION flag.
        /// </summary>
        /// <param name="pHandle">The handle of the process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointer(IntPtr pHandle, string mname, long sAddress, params int[] offsets)
        : base(pHandle, mname, sAddress, offsets)
        {
            TargetSize = GenericBitConverter.GetTypeSize(typeof(T));
        }

        /// <summary>
        /// Create a DeepPointer from a process and module name.
        /// </summary>
        /// <param name="proc">The process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointer(Process proc, string mname, long sAddress, params int[] offsets)
        : base(proc, mname, sAddress, offsets)
        {
            TargetSize = GenericBitConverter.GetTypeSize(typeof(T));
        }

        /// <summary>
        /// Create a DeepPointer from a process name and module name.
        /// </summary>
        /// <param name="pname">The name of the process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointer(string pname, string mname, long sAddress, params int[] offsets)
        : base(pname, mname, sAddress, offsets)
        {
            TargetSize = GenericBitConverter.GetTypeSize(typeof(T));
        }

        /// <summary>
        /// Get the value this object points to as the specified type, if possible.
        /// </summary>
        /// <returns></returns>
        public T GetValue()
        {
            // Get the bytes and handle the null case
            byte[] bytes = GetBytes(TargetSize);
            if (bytes == null) return default(T);

            return GenericBitConverter.ToStruct<T>(bytes);
        }

        /// <summary>
        /// Write the value this object points to, if possible.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>A bool indicating the success of this operation</returns>
        public bool SetValue(T value)
        {
            byte[] bytes = GenericBitConverter.ToBytes(value);
            return SetBytes(bytes);
        }
    }

    /// <summary>
    /// Represents a system of muliple nested pointers to a null-terminated string.
    /// </summary>
    public class DeepPointerStr : DeepPointerBase
    {
        /// <summary>
        /// Create a DeepPointerStr from an already known handle and module address.
        /// </summary>
        /// <param name="pHandle">The handle of the process to read from.</param>
        /// <param name="mbAddress">The base address of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerStr(IntPtr pHandle, long mbAddress, long sAddress, params int[] offsets)
        : base(pHandle, mbAddress, sAddress, offsets) { }

        /// <summary>
        /// Create a DeepPointerStr from a process and module name.
        /// </summary>
        /// <param name="proc">The process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerStr(Process proc, string mname, long sAddress, params int[] offsets)
        : base(proc, mname, sAddress, offsets) { }

        /// <summary>
        /// Create a DeepPointerStr from a process name and module name.
        /// </summary>
        /// <param name="pname">The name of the process to read from.</param>
        /// <param name="mname">The name of a specific module in the process.</param>
        /// <param name="sAddress">The static address to start reading from.</param>
        /// <param name="offsets">The offsets between each read in memory.</param>
        public DeepPointerStr(string pname, string mname, long sAddress, params int[] offsets)
        : base(pname, mname, sAddress, offsets) { }

        /// <summary>
        /// Get the value this object points to as string, if possible.
        /// </summary>
        /// <param name="maxLength">
        /// The maximum amount of bytes to grab. If the string is null-terminated,
        /// the length of the string will be smaller.
        /// </param>
        /// <returns></returns>
        public string GetValue(int maxLength = 256)
        {
            // Get the dynamic address
            long dyn = GetDynamicAddress();
            if (dyn == 0) return null;

            StringBuilder builder = new StringBuilder(maxLength);
            byte singleChar;

            while (builder.Length < maxLength)
            {
                singleChar = MemoryManager.ReadSingleByte(ProcHandle, dyn++);

                if (singleChar == 0) break;      // Null byte, string ends
                else builder.Append(singleChar); // Add char to string
            }

            return builder.ToString();
        }

        /// <summary>
        /// Get the value this object points to as UTF-16 string, if possible.
        /// </summary>
        /// <param name="maxLength">
        /// The maximum amount of characters to grab. If the string is null-terminated,
        /// the length of the string will be smaller.
        /// </param>
        /// <returns></returns>
        public string GetValueUnicode(int maxLength = 256)
        {
            // Get the dynamic address
            long dyn = GetDynamicAddress();
            if (dyn == 0) return null;

            StringBuilder builder = new StringBuilder(maxLength);
            ushort singleChar;

            while (builder.Length < 2*maxLength)
            {
                singleChar = BitConverter.ToUInt16(MemoryManager.ReadByteArray(ProcHandle, dyn, 2), 0);
                dyn += 2;

                if (singleChar == 0) break;      // Null char, string ends
                else builder.Append(char.ConvertFromUtf32(singleChar)); // Add char to string
            }

            return builder.ToString();
        }
    }
}