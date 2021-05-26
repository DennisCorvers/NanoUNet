/*
 *  Lightweight UDP sockets abstraction for rapid implementation of message-oriented protocols
 *  Copyright (c) 2019 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace NanoSockets
{
    public enum SocketStatus : int
    {
        OK = 0,
        Error = -1
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct Socket
    {
        [FieldOffset(0)]
        private readonly long handle;

        public bool IsCreated
        {
            get
            {
                return handle > 0;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 18)]
    public struct Address : IEquatable<Address>
    {
        public static readonly Address Any = CreateFromIpPort("0.0.0.0", 0);
        public static readonly Address IPv4Loopback = CreateFromIpPort("127.0.0.1", 0);
        public static readonly Address IPv4Broadcast = CreateFromIpPort("255.255.255.255", 0);
        public static readonly Address IPv4None = IPv4Broadcast;

        public static readonly Address IPv6Any = CreateFromIpPort("::", 0);
        public static readonly Address IPv6Loopback = CreateFromIpPort("::1", 0);
        public static readonly Address IPv6None = IPv6Any;

        [FieldOffset(0)]
        private ulong address0;
        [FieldOffset(8)]
        private ulong address1;
        [FieldOffset(16)]
        private ushort port;

        public ushort Port
        {
            get => port;
            set => port = value;
        }

        public Address(string ipString, ushort port)
        {
            // Trick to allow constructor usage...
            var other = default(Address);
            NanoSocketAPI.SetIP(ref other, ipString);

            address0 = other.address0;
            address1 = other.address1;
            this.port = port;
        }

        public Address(System.Net.IPEndPoint ipEndPoint)
        {
            // Trick to allow constructor usage...
            var other = default(Address);
            NanoSocketAPI.SetIP(ref other, ipEndPoint.Address.ToString());

            address0 = other.address0;
            address1 = other.address1;
            port = (ushort)ipEndPoint.Port;
        }

        public bool Equals(Address other)
        {
            return address0 == other.address0 && address1 == other.address1 && port == other.port;
        }

        public override bool Equals(object obj)
        {
            if (obj is Address)
                return Equals((Address)obj);

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 31 + address0.GetHashCode();
            hash = hash * 31 + address1.GetHashCode();
            hash = hash * 31 + port.GetHashCode();

            return hash;
        }

        public override string ToString()
        {
            StringBuilder ip = new StringBuilder(64);

            NanoSocketAPI.GetIP(ref this, ip, 64);

            return $"IP: {ip} Port: {port}";
        }

        public static Address CreateFromIpPort(string ip, ushort port)
        {
            Address address = default(Address);

            NanoSocketAPI.SetIP(ref address, ip);
            address.port = port;

            return address;
        }
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class NanoSocketAPI
    {
#if __IOS__ || UNITY_IOS && !UNITY_EDITOR
			private const string nativeLibrary = "__Internal";
#else
        private const string nativeLibrary = "nanosockets";
#endif

        public const int hostNameSize = 1025;

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus Initialize();

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_deinitialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Deinitialize();

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern Socket Create(int sendBufferSize, int receiveBufferSize);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Destroy(ref Socket socket);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_bind", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Bind(Socket socket, IntPtr address);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_bind", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Bind(Socket socket, ref Address address);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_connect", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Connect(Socket socket, ref Address address);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_set_option", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetOption(Socket socket, int level, int optionName, ref int optionValue, int optionLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_get_option", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetOption(Socket socket, int level, int optionName, ref int optionValue, ref int optionLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_set_nonblocking", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetNonBlocking(Socket socket);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_set_dontfragment", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetDontFragment(Socket socket);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_poll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Poll(Socket socket, long timeout);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Send(Socket socket, IntPtr address, IntPtr buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Send(Socket socket, IntPtr address, byte[] buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Send(Socket socket, ref Address address, IntPtr buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_send", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Send(Socket socket, ref Address address, byte[] buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_receive", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Receive(Socket socket, IntPtr address, IntPtr buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_receive", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Receive(Socket socket, IntPtr address, byte[] buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_receive", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Receive(Socket socket, ref Address address, IntPtr buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_receive", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Receive(Socket socket, ref Address address, byte[] buffer, int bufferLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_get", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetAddress(Socket socket, ref Address address);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_is_equal", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus IsEqual(ref Address left, ref Address right);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_set_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetIP(ref Address address, IntPtr ip);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_set_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetIP(ref Address address, string ip);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_get_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetIP(ref Address address, IntPtr ip, int ipLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_get_ip", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetIP(ref Address address, StringBuilder ip, int ipLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_set_hostname", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetHostName(ref Address address, IntPtr name);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_set_hostname", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus SetHostName(ref Address address, string name);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_get_hostname", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetHostName(ref Address address, IntPtr name, int nameLength);

        [DllImport(nativeLibrary, EntryPoint = "nanosockets_address_get_hostname", CallingConvention = CallingConvention.Cdecl)]
        public static extern SocketStatus GetHostName(ref Address address, StringBuilder name, int nameLength);
    }
}