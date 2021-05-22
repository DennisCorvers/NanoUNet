using NanoSockets;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NanoUNet
{
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
}
