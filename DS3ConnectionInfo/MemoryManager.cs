using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// A class to manage process memory.
    /// </summary>
    public class MemoryManager : IDisposable
    {
        #region static

        /// <summary>
        /// The standard flags for a MemoryManager object. Includes query of information and memory modification.
        /// </summary>
        public static ProcessAccessFlags DefaultAccessFlags =
            ProcessAccessFlags.QueryInformation |
            ProcessAccessFlags.VmWrite |
            ProcessAccessFlags.VmRead |
            ProcessAccessFlags.VmOperation;

        /// <summary>
        /// Gets the base address of a module in a specific process as a pointer.
        /// </summary>
        /// <param name="proc">The process containing the module.</param>
        /// <param name="modulname">The module name.</param>
        /// <returns></returns>
        public static IntPtr GetModuleAddressPtr(Process proc, string modulname)
        {
            return (from module in proc.Modules.Cast<ProcessModule>()
                    where module.ModuleName == modulname
                    select module.BaseAddress).FirstOrDefault();
        }

        /// <summary>
        /// Gets the base address of a module in a specific process.
        /// </summary>
        /// <param name="proc">The process containing the module.</param>
        /// <param name="modulname">The module name.</param>
        /// <returns></returns>
        public static long GetModuleAddress(Process proc, string modulname)
        {
            return GetModuleAddressPtr(proc, modulname).ToInt64();
        }

        /// <summary>
        /// Reads a byte array of arbitrary length from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <param name="nBytes">The number of bytes to read.</param>
        /// <returns>If the read worked, return the bytes; otherwise null.</returns>
        public static byte[] ReadByteArray(IntPtr hProcess, long memaddress, int nBytes)
        {
            // Create a buffer to hold the data
            byte[] buffer = new byte[nBytes];

            // Read memory to the buffer and get the amount of read bytes
            var numRead = IntPtr.Zero;
            WinAPI.ReadProcessMemory(hProcess, memaddress, buffer, nBytes, out numRead);

            // Unsuccessful read, we don't have access or the address is wrong
            if (numRead.ToInt32() != nBytes)
                throw new UnauthorizedAccessException("Could not read memory");

            // Everything went okay, so return
            return buffer;
        }

        /// <summary>
        /// Writes a byte array of arbitrary length to the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        public static void WriteByteArray(IntPtr hProcess, long memaddress, byte[] bytes)
        {
            var nBytes = bytes.Length;

            var numWrite = IntPtr.Zero;
            WinAPI.WriteProcessMemory(hProcess, memaddress, bytes, nBytes, out numWrite);

            // Unsuccessful write, we don't have access or the address is wrong
            if (numWrite.ToInt32() != nBytes)
                throw new UnauthorizedAccessException("Could not write memory");
        }

        /// <summary>
        /// Reads a single byte from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to write from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns>If the read worked, return the byte; otherwise null.</returns>
        public static byte ReadSingleByte(IntPtr hProcess, long memaddress)
        {
            return ReadByteArray(hProcess, memaddress, 1)[0];
        }

        /// <summary>
        /// Writes a single byte to the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to write from.</param>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="value">The byte to write.</param>
        public static void WriteSingleByte(IntPtr hProcess, long memaddress, byte value)
        {
            WriteByteArray(hProcess, memaddress, new byte[1] { value });
        }

        /// <summary>
        /// Reads a 64-bit int from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns></returns>
        internal static long ReadInt64(IntPtr hProcess, long memaddress)
        {
            // Read the bytes using the other function
            byte[] bytes = ReadByteArray(hProcess, memaddress, 8);
            return BitConverter.ToInt64(bytes, 0);
        }

        #endregion static

        /// <summary>
        /// The loaded process.
        /// </summary>
        public Process Process { get; private set; }
        /// <summary>
        /// The handle of the loaded process.
        /// </summary>
        public IntPtr ProcHandle { get; private set; }
        /// <summary>
        /// True if the process has exited.
        /// </summary>
        public bool HasExited => Process.HasExited;

        /// <summary>
        /// Create a new MemoryManager.
        /// </summary>
        public MemoryManager()
        {
            Process = null;
            ProcHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Load a process by name.
        /// </summary>
        /// <param name="exeName">The name of the executable.</param>
        public void OpenProcess(string exeName)
        {
            Process = Process.GetProcessesByName(exeName)[0];
            ProcHandle = WinAPI.OpenProcess(DefaultAccessFlags, false, Process.Id);
        }

        /// <summary>
        /// Load a process by name.
        /// </summary>
        /// <param name="exeName">The name of the executable.</param>
        /// <param name="access">The required access rights.</param>
        public void OpenProcess(string exeName, ProcessAccessFlags access)
        {
            Process = Process.GetProcessesByName(exeName)[0];
            ProcHandle = WinAPI.OpenProcess(access, false, Process.Id);
        }

        /// <summary>
        /// Load a process from an existing instance.
        /// </summary>
        /// <param name="proc">The process to open.</param>
        public void OpenProcess(Process proc)
        {
            Process = proc;
            ProcHandle = WinAPI.OpenProcess(DefaultAccessFlags, false, proc.Id);
        }

        /// <summary>
        /// Load a process from an existing instance.
        /// </summary>
        /// <param name="proc">The process to open.</param>
        /// <param name="access">The required access rights.</param>
        public void OpenProcess(Process proc, ProcessAccessFlags access)
        {
            Process = proc;
            ProcHandle = WinAPI.OpenProcess(access, false, proc.Id);
        }

        /// <summary>
        /// Close the link between the memory manager and the process.
        /// </summary>
        public void ReleaseProcess()
        {
            if (Process != null)
            {
                Process.Dispose();
                Process = null;
            }

            WinAPI.CloseHandle(ProcHandle);
            ProcHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Get a specific module from its name.
        /// </summary>
        /// <param name="moduleName">The name of the module to get.</param>
        /// <returns>The module itself.</returns>
        public ProcessModule GetProcessModule(string moduleName)
        {
            return (from module in Process.Modules.Cast<ProcessModule>()
                    where module.ModuleName == moduleName
                    select module).FirstOrDefault();
        }

        /// <summary>
        /// Get the address of a specific module from its name.
        /// </summary>
        /// <param name="moduleName">The name of the module in question.</param>
        /// <returns>A 64 bit integer containing the address of the module.</returns>
        public long GetModuleAddress(string moduleName)
        {
            return GetModuleAddress(Process, moduleName);
        }

        /// <summary>
        /// Reads a byte array of arbitrary length from the specified address.
        /// </summary>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <param name="nBytes">The number of bytes to read.</param>
        /// <returns>If the read worked, return the bytes; otherwise null.</returns>
        public byte[] ReadByteArray(long memaddress, int nBytes)
        {
            if (HasExited) // Process is out, cannot read
                throw new InvalidOperationException("Process has exited, cannot read");

            return ReadByteArray(ProcHandle, memaddress, nBytes);
        }

        /// <summary>
        /// Writes a byte array of arbitrary length to the specified address.
        /// </summary>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        public void WriteByteArray(long memaddress, byte[] bytes)
        {
            if (HasExited) // Process is out, cannot read
                throw new InvalidOperationException("Process has exited, cannot write");

            WriteByteArray(ProcHandle, memaddress, bytes);
        }

        /// <summary>
        /// Reads a single byte from the specified address.
        /// </summary>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns>If the read worked, return the byte; otherwise null.</returns>
        public byte ReadSingleByte(long memaddress)
        {
            if (HasExited) // Process is out, cannot read
                throw new InvalidOperationException("Process has exited, cannot write");

            return ReadSingleByte(ProcHandle, memaddress);
        }

        /// <summary>
        /// Writes a single byte to the specified address.
        /// </summary>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="value">The byte to write.</param>
        public void WriteSingleByte(long memaddress, byte value)
        {
            if (HasExited) // Process is out, cannot read
                throw new InvalidOperationException("Process has exited, cannot write");

            WriteSingleByte(ProcHandle, memaddress, value);
        }

        /// <summary>
        /// Reads a 64-bit int from the specified address.
        /// </summary>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns></returns>
        internal long ReadInt64(long memaddress)
        {
            if (HasExited) // Process is out, cannot read
                throw new InvalidOperationException("Process has exited, cannot write");

            return ReadInt64(ProcHandle, memaddress);
        }

        /// <summary>
        /// Read a generic value from a memory address.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="memaddress"></param>
        /// <returns></returns>
        public T ReadGeneric<T>(long memaddress) where T : struct
        {
            int size = GenericBitConverter.GetTypeSize(typeof(T));
            byte[] bytes = ReadByteArray(memaddress, size);
            return GenericBitConverter.ToStruct<T>(bytes);
        }

        /// <summary>
        /// Write a generic value to a memory address.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="memaddress">The memory address to write to.</param>
        /// <param name="value">The generic value.</param>
        /// <returns></returns>
        public void WriteGeneric<T>(long memaddress, T value) where T : struct
        {
            byte[] bytes = GenericBitConverter.ToBytes(value);
            WriteByteArray(memaddress, bytes);
        }

        /// <summary>
        /// Read a generic value from a pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="moduleBaseAddress"></param>
        /// <param name="staticAddress"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        public T ReadGenericPtr<T>(long moduleBaseAddress, long staticAddress, params int[] offsets) where T : struct
        {
            return new DeepPointer<T>(ProcHandle, moduleBaseAddress, staticAddress, offsets).GetValue();
        }

        /// <summary>
        /// Read a generic value from a pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="modulname"></param>
        /// <param name="staticAddress"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        public T ReadGenericPtr<T>(string modulname, long staticAddress, params int[] offsets) where T : struct
        {
            return new DeepPointer<T>(ProcHandle, modulname, staticAddress, offsets).GetValue();
        }

        /// <summary>
        /// Write a generic value to a pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="moduleBaseAddress"></param>
        /// <param name="staticAddress"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        public bool WriteGenericPtr<T>(T value, long moduleBaseAddress, long staticAddress, params int[] offsets) where T : struct
        {
            return new DeepPointer<T>(ProcHandle, moduleBaseAddress, staticAddress, offsets).SetValue(value);
        }

        /// <summary>
        /// Write a generic value to a pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="modulname"></param>
        /// <param name="staticAddress"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        public bool WriteGenericPtr<T>(T value, string modulname, long staticAddress, params int[] offsets) where T : struct
        {
            return new DeepPointer<T>(ProcHandle, modulname, staticAddress, offsets).SetValue(value);
        }

        private bool disposed = false;

        /// <summary>
        /// Free resources used by this object.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    WinAPI.CloseHandle(ProcHandle);
                }
                Process.Dispose();

                disposed = true;
            }
        }

        /// <summary>
        /// Free resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}