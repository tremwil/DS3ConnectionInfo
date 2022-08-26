using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DS3ConnectionInfo.WinAPI;
using System.Reflection;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// A class to manage process memory.
    /// </summary>
    public static class MemoryManager
    {
        public static long GetModuleBase(this Process proc, string moduleName)
        {
            for (int i = 0; i < proc.Modules.Count; i++)
            {
                if (proc.Modules[i].ModuleName == moduleName)
                    return (long)proc.Modules[i].BaseAddress;
            }
            return 0;
        }

        /// <summary>
        /// Extension method which converts a string of hex characters into a byte array.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] HexToBytes(this string hex)
        {
            hex = hex.ToUpper();
            byte[] b = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length / 2; i++)
            {
                int v = (byte)(hex[i << 1] - (hex[i << 1] < 'A' ? 48 : 55));
                b[i] = (byte)(v << 4 | (hex[i << 1 | 1] - (hex[i << 1 | 1] < 'A' ? 48 : 55)));
            }
            return b;
        }

        /// <summary>
        /// Reads a byte array of arbitrary length from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <param name="nBytes">The number of bytes to read.</param>
        /// <returns>If the read worked, return the bytes; otherwise null.</returns>
        public static byte[] ReadByteArray(IntPtr pHandle, long memaddress, int nBytes)
        {
            // Create a buffer to hold the data
            byte[] buffer = new byte[nBytes];

            // Read memory to the buffer and get the amount of read bytes
            int numRead = 0;
            bool s = Kernel32.ReadProcessMemory(pHandle, memaddress, buffer, nBytes, out numRead);

            // Unsuccessful read, we don't have access or the address is wrong
            if (s && numRead != nBytes)
                throw new UnauthorizedAccessException("ReadProcessMemory Failed");

            // Everything went okay, so return
            return buffer;
        }

        /// <summary>
        /// Writes a byte array of arbitrary length to the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        public static void WriteByteArray(IntPtr pHandle, long memaddress, byte[] bytes)
        {
            var nBytes = bytes.Length;

            int numWrite = 0;
            bool s = Kernel32.WriteProcessMemory(pHandle, memaddress, bytes, nBytes, out numWrite);

            // Unsuccessful write, we don't have access or the address is wrong
            if (s && numWrite != nBytes)
                throw new UnauthorizedAccessException("WriteProcessMemory Failed");
        }

        /// <summary>
        /// Reads a single byte from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to write from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns>If the read worked, return the byte; otherwise null.</returns>
        public static byte ReadByte(IntPtr pHandle, long memaddress)
        {
            return ReadByteArray(pHandle, memaddress, 1)[0];
        }

        /// <summary>
        /// Writes a single byte to the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to write from.</param>
        /// <param name="memaddress">The memory address we start writing from.</param>
        /// <param name="value">The byte to write.</param>
        public static void WriteByte(IntPtr pHandle, long memaddress, byte value)
        {
            WriteByteArray(pHandle, memaddress, new byte[1] { value });
        }

        /// <summary>
        /// Reads a 64-bit int from the specified address.
        /// </summary>
        /// <param name="hProcess">The handle of the process to read from.</param>
        /// <param name="memaddress">The memory address we start reading from.</param>
        /// <returns></returns>
        internal static long ReadInt64(IntPtr pHandle, long memaddress)
        {
            // Read the bytes using the other function
            byte[] bytes = ReadByteArray(pHandle, memaddress, 8);
            return BitConverter.ToInt64(bytes, 0);
        }

        /// <summary>
        /// Read a generic value from a memory address.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="memaddress"></param>
        /// <returns></returns>
        public static T ReadGeneric<T>(IntPtr pHandle, long memaddress) where T : struct
        {
            int size = GenericBitConverter.GetTypeSize(typeof(T));
            byte[] bytes = ReadByteArray(pHandle, memaddress, size);
            return GenericBitConverter.ToStruct<T>(bytes);
        }

        /// <summary>
        /// Write a generic value to a memory address.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="memaddress">The memory address to write to.</param>
        /// <param name="value">The generic value.</param>
        /// <returns></returns>
        public static void WriteGeneric<T>(IntPtr pHandle, long memaddress, T value) where T : struct
        {
            byte[] bytes = GenericBitConverter.ToBytes(value);
            WriteByteArray(pHandle, memaddress, bytes);
        }

        /// <summary>
        /// A direct address to the value. Do not store this address to reliably
        /// get data since it can change at any time.
        /// </summary>
        /// <returns>The current address to the data, otherwise 0</returns>
        public static long GetDynamicAddress(IntPtr pHandle, long address, int[] offsets)
        {
            try
            {
                // Iteratively update the current address by moving up the pointers chain
                foreach (long offset in offsets)
                    address = ReadInt64(pHandle, address) + offset;

                return address;
            }
            catch (UnauthorizedAccessException)
            {
                return 0; // Pointer is probably changing addresses
            }
        }

        /// <summary>
        /// A direct address to the value. Do not store this address to reliably
        /// get data since it can change at any time.
        /// </summary>
        /// <returns>The current address to the data, otherwise 0</returns>
        public static long GetDynamicAddress(IntPtr pHandle, string module, long address, int[] offsets)
        {
            try
            {
                // Iteratively update the current address by moving up the pointers chain
                foreach (long offset in offsets)
                    address = ReadInt64(pHandle, address) + offset;

                return address;
            }
            catch (UnauthorizedAccessException)
            {
                return 0; // Pointer is probably changing addresses
            }
        }

        /// <summary>
        /// Read a generic value from a pointer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="moduleBaseAddress"></param>
        /// <param name="staticAddress"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        public static T ReadGenericPtr<T>(IntPtr pHandle, long baseAddress, params int[] offsets) where T : struct
        {
            long addr = GetDynamicAddress(pHandle, baseAddress, offsets);
            return ReadGeneric<T>(pHandle, addr);
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
        public static void WriteGenericPtr<T>(IntPtr pHandle, T value, long baseAddress, params int[] offsets) where T : struct
        {
            long addr = GetDynamicAddress(pHandle, baseAddress, offsets);
            WriteGeneric(pHandle, addr, value);
        }

        public static string ReadString(IntPtr pHandle, long addr, int bLen, Encoding encoding)
        {
            byte[] b = ReadByteArray(pHandle, addr, bLen);
            return encoding.GetString(b);
        }

        public static string ReadStringNT(IntPtr pHandle, long addr, int maxLen, Encoding encoding)
        {
            string s = ReadString(pHandle, addr, maxLen, encoding);
            int i = s.IndexOf('\0');
            if (i >= 0) s = s.Remove(i);
            return s;
        }

        public static string ReadStringPtr(IntPtr pHandle, int bLen, Encoding encoding, long baseAddress, params int[] offsets)
        {
            long addr = GetDynamicAddress(pHandle, baseAddress, offsets);
            return ReadString(pHandle, addr, bLen, encoding);
        }

        public static string ReadStringPtrNT(IntPtr pHandle, int maxLen, Encoding encoding, long baseAddress, params int[] offsets)
        {
            long addr = GetDynamicAddress(pHandle, baseAddress, offsets);
            return ReadStringNT(pHandle, addr, maxLen, encoding);
        }

        /// <summary>
        /// Execute code after loading the given arguments into the processes's memory.
        /// Pointers to those arguments are then inserted in the original assembly code.
        /// </summary>
        /// <param name="asm"></param>
        /// <param name="arguments"></param>
        /// <param name="syncWait">Sync timeout in milliseconds (0ms = async).</param>
        public static void ExecuteFunction(IntPtr pHandle, byte[] asm, Dictionary<int, object> replacements = null, Dictionary<int, object> arguments = null, int timeout = 3000)
        {
            if (arguments == null)
                arguments = new Dictionary<int, object>();

            foreach (int idx in replacements.Keys)
            {
                byte[] replacement = GenericBitConverter.ToBytes(replacements[idx]);
                Array.Copy(replacement, 0, asm, idx, replacement.Length);
            }

            Dictionary<int, byte[]> argData = new Dictionary<int, byte[]>();
            foreach (int idx in arguments.Keys)
                argData[idx] = GenericBitConverter.ToBytes(arguments[idx]);

            int argSz = argData.Sum(p => p.Value.Length);

            long asmAddr = 0, argAddr = 0;
            asmAddr = Kernel32.VirtualAllocEx(pHandle, 0, asm.Length, MemOpType.Reserve | MemOpType.Commit, MemProtect.ExecuteReadWrite);
            if (asmAddr == 0) 
                throw new Exception("VirtualAllocEx failed");

            if (arguments.Count > 0)
            {
                argAddr = Kernel32.VirtualAllocEx(pHandle, 0, argSz, MemOpType.Reserve | MemOpType.Commit, MemProtect.ExecuteReadWrite);
                if (argAddr == 0)
                {
                    Kernel32.VirtualFreeEx(pHandle, asmAddr, 0, MemOpType.Release);
                    throw new Exception("VirtualAllocEx failed");
                }
            }
            try
            {
                long cAddr = argAddr;
                foreach (int idx in arguments.Keys)
                {
                    WriteByteArray(pHandle, cAddr, argData[idx]);
                    Array.Copy(BitConverter.GetBytes(cAddr), 0, asm, idx, 8);
                    cAddr += argData[idx].Length;
                }
                WriteByteArray(pHandle, asmAddr, asm);

                IntPtr hThread = Kernel32.CreateRemoteThread(pHandle, IntPtr.Zero, 0, asmAddr, IntPtr.Zero, 0, out int threadId);
                if (hThread != IntPtr.Zero && timeout != 0)
                    Kernel32.WaitForSingleObject(hThread, timeout);
            }
            finally
            {
                Kernel32.VirtualFreeEx(pHandle, asmAddr, 0, MemOpType.Release);
                if (argAddr != 0) Kernel32.VirtualFreeEx(pHandle, argAddr, 0, MemOpType.Release);
            }
        }
    }
}