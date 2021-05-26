using NanoSockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NanoUNet
{
    public sealed class NanoSocket : IDisposable
    {
        /// <summary>
        /// Maximum size of an UDP datagram.
        /// </summary>
        public const int MaxDatagramSize = 65507;

        private const int DefaultSendBufSize = 65536;
        private const int DefaultRecvBufSize = 65536;

#pragma warning disable IDE0032, IDE0044
        private NanoSockets.Socket m_socketHandle;
        private AddressFamily m_addressFamily;
        private int m_recvBufferSize = DefaultRecvBufSize;
        private int m_sendBufferSize = DefaultSendBufSize;

        private bool m_isConnected;
        private bool m_isBound;
        private bool m_isCleanedUp;
#pragma warning restore IDE0032, IDE0044

        static NanoSocket()
        {
            NanoSocketAPI.Initialize();
        }

        /// <summary>
        /// Indicates if the <see cref="NanoSocket"/> is connected.
        /// </summary>
        public bool Connected
            => m_isConnected;
        /// <summary>
        /// Indicates if the <see cref="NanoSocket"/> is bound to a port.
        /// </summary>
        public bool IsBound
            => m_isBound;
        /// <summary>
        /// Gets or sets a value that specifies the Time to Live (TTL) value of Internet
        /// Protocol (IP) packets sent by the <see cref="NanoSocket"/>.
        /// </summary>
        public byte Ttl
        {
            get => (byte)GetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive);
            set => SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, value);
        }
        /// <summary>
        /// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="NanoSocket"/>
        /// allows Internet Protocol (IP) datagrams to be fragmented.
        /// </summary>
        public bool DontFragment
        {
            get => GetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment) == 1;
            set => SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, value);
        }
        /// <summary>
        /// Gets or sets a <see cref="bool"/> value that specifies whether outgoing multicast
        /// packets are delivered to the sending application.
        /// </summary>
        public bool MulticastLoopback
        {
            get => GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback) == 1;
            set => SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, value);
        }
        /// <summary>
        /// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="NanoSocket"/>
        /// may send or receive broadcast packets.
        /// </summary>
        public bool EnableBroadcast
        {
            get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast) == 1;
            set => SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, value);
        }
        /// <summary>
        /// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="NanoSocket"/>
        /// allows only one client to use a port.
        /// </summary>
        public bool ExclusiveAddressUse
        {
            get => GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse) == 1;
            set => SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, value);
        }

        /// <summary>
        /// Creates a new <see cref="NanoSocket"/>.
        /// </summary>
        public NanoSocket(AddressFamily family = AddressFamily.InterNetwork)
        {
            ValidateAddressFamily(family);

            m_addressFamily = family;

            m_socketHandle = NanoSocketAPI.Create(m_sendBufferSize, m_recvBufferSize);
        }

        /// <summary>
        /// Creates a new <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="receiveBufferSize">The size of the receive buffer.</param>
        /// <param name="sendBufferSize">The size of the send buffer.</param>
        public NanoSocket(int receiveBufferSize, int sendBufferSize, AddressFamily family = AddressFamily.InterNetwork)
        {
            ValidateAddressFamily(family);

            if (receiveBufferSize < 1)
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize), "Value must be greater than zero.");

            if (sendBufferSize < 1)
                throw new ArgumentOutOfRangeException(nameof(sendBufferSize), "Value must be greater than zero.");

            m_addressFamily = family;
            m_recvBufferSize = receiveBufferSize;
            m_sendBufferSize = sendBufferSize;

            m_socketHandle = NanoSocketAPI.Create(m_sendBufferSize, m_recvBufferSize);
        }

        ~NanoSocket()
            => Dispose(false);

        /// <summary>
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="address">The Ip Address of the remote host.</param>
        /// <param name="port">The port of the remote host.</param>
        public SocketError Connect(Address address)
        {
            ThrowIfDisposed();

            var status = (SocketError)NanoSocketAPI.Connect(m_socketHandle, ref address);

            m_isConnected = status == SocketError.Success;
            return status;
        }

        /// <summary>
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="hostname">The hostname of the remote host.</param>
        /// <param name="ip">The port of the remote host.</param>
        public SocketError Connect(string hostname, ushort port)
        {
            if (hostname == null)
                throw new ArgumentNullException(nameof(hostname));

            var nanoAddress = default(Address);
            nanoAddress.Port = port;

            if (NanoSocketAPI.SetHostName(ref nanoAddress, hostname) != 0)
                throw new ArgumentOutOfRangeException(nameof(hostname), $"Unable to resolve {hostname}.");

            return Connect(nanoAddress);
        }

        /// <summary>
        /// Binds the <see cref="UdpSocket"/> to a specified port.
        /// </summary>
        /// <param name="port">The port to bind the <see cref="NanoSocket"/> to.</param>
        public SocketError Bind(ushort port)
        {
            ThrowIfDisposed();
            ThrowIfAlreadyBound();

            Address tempEP;

            if (m_addressFamily == AddressFamily.InterNetwork)
                tempEP = Address.Any;
            else
                tempEP = Address.IPv6Any;

            tempEP.Port = port;

            var status = (SocketError)NanoSocketAPI.Bind(m_socketHandle, ref tempEP);
            m_isBound = status == SocketError.Success;

            return status;
        }

        /// <summary>
        /// Binds the <see cref="NanoSocket"/> to a specified endpoint.
        /// </summary>
        public SocketError Bind(IPEndPoint localEP)
        {
            ThrowIfDisposed();
            ThrowIfAlreadyBound();

            if (localEP == null)
                throw new ArgumentNullException(nameof(localEP));

            if (localEP.AddressFamily != m_addressFamily)
                throw new ArgumentException($"{nameof(localEP)} AddressFamily is not compatible with Sockets' AddressFamily.");

            var nanoAddress = new Address(localEP);
            var status = (SocketError)NanoSocketAPI.Bind(m_socketHandle, ref nanoAddress);
            m_isBound = status == SocketError.Success;

            return status;
        }


        /// <summary>
        /// Sets the specified Socket option setting to the specified boolean value.
        /// </summary>
        /// <param name="optionLevel">One of the <see cref="SocketOptionLevel"/> values.</param>
        /// <param name="optionName">One of the <see cref="SocketOptionName"/> values.</param>
        /// <param name="optionValue">A value of the option.</param>
        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
        {
            SetSocketOption(optionLevel, optionName, optionValue ? 1 : 0);
        }

        /// <summary>
        /// Sets the specified Socket option setting to the specified integer value.
        /// </summary>
        /// <param name="optionLevel">One of the <see cref="SocketOptionLevel"/> values.</param>
        /// <param name="optionName">One of the <see cref="SocketOptionName"/> values.</param>
        /// <param name="optionValue">A value of the option.</param>
        public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
        {
            ThrowIfDisposed();

            var tempValue = optionValue;

            if (NanoSocketAPI.SetOption(m_socketHandle, (int)optionLevel, (int)optionName, ref tempValue, sizeof(int)) != 0)
                throw new InvalidOperationException(ErrorCodes.SetSocketOptionError);
        }

        /// <summary>
        /// Returns the specified Socket option setting, represented as an integer.
        /// </summary>
        /// <param name="optionLevel">One of the <see cref="SocketOptionLevel"/> values.</param>
        /// <param name="optionName">One of the <see cref="SocketOptionName"/> values.</param>
        public int GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
        {
            ThrowIfDisposed();

            int tempValue = 0;
            int tempLength = sizeof(int);

            if (NanoSocketAPI.GetOption(m_socketHandle, (int)optionLevel, (int)optionName, ref tempValue, ref tempLength) != 0)
                throw new InvalidOperationException(ErrorCodes.GetSocketOptionError);

            return tempValue;
        }

        /// <summary>
        /// Disposes the <see cref="NanoSocket"/> and frees the underlying resources.
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (m_isCleanedUp)
                return;

            if (disposing)
                GC.SuppressFinalize(this);

            // Free unmanaged resources.
            NanoSocketAPI.Destroy(ref m_socketHandle);

            m_isCleanedUp = true;
            m_isConnected = false;
            m_isBound = false;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="NanoSocket"/> class.
        /// </summary>
        public void Dispose()
            => Dispose(true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (m_isCleanedUp)
                ThrowObjectDisposedException();

            void ThrowObjectDisposedException() => throw new ObjectDisposedException(GetType().FullName);
        }

        private void ThrowIfAlreadyBound()
        {
            if (m_isBound)
                ThrowSocketAlreadyyBoundException();

            void ThrowSocketAlreadyyBoundException() => throw new InvalidOperationException("Socket is already bound.");
        }

        private void ValidateAddressFamily(AddressFamily family)
        {
            if (family != AddressFamily.InterNetwork &&
                family != AddressFamily.InterNetworkV6)
            {
                throw new ArgumentException("Invalid AddressFamily for UDP protocol.", nameof(family));
            }
        }

        private static class ErrorCodes
        {
            public const string InvalidProtocolVersion = "This protocol version is not supported";
            public const string GetSocketOptionError = "GetSocketOption returned an error.";
            public const string SetSocketOptionError = "SetSocketOption returned an error.";
        }
    }
}
