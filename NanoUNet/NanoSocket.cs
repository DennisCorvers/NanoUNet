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
        private AddressFamily m_family;
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
        /// Creates a new <see cref="NanoSocket"/>.
        /// </summary>
        public NanoSocket(AddressFamily family = AddressFamily.InterNetwork)
        {
            ValidateAddressFamily(family);

            m_family = family;

            CreateNewSocketIfNeeded();
        }

        /// <summary>
        /// Creates a new <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="receiveBufferSize">The size of the receive buffer.</param>
        /// <param name="sendBufferSize">The size of the send buffer.</param>
        public NanoSocket(int receiveBufferSize, int sendBufferSize, AddressFamily family = AddressFamily.InterNetwork)
        {
            ValidateAddressFamily(family);

            if (receiveBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize), "Value must be greater than zero.");

            if (sendBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(sendBufferSize), "Value must be greater than zero.");

            m_family = family;
            m_recvBufferSize = receiveBufferSize;
            m_sendBufferSize = sendBufferSize;

            CreateNewSocketIfNeeded();
        }


        /// <summary>
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="address">The Ip Address of the remote host.</param>
        /// <param name="port">The port of the remote host.</param>
        public SocketStatus Connect(Address address)
        {
            ThrowIfDisposed();

            var status = (SocketStatus)NanoSocketAPI.Connect(m_socketHandle, ref address);

            m_isConnected = status == SocketStatus.OK;
            return status;
        }

        /// <summary>
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="hostname">The hostname of the remote host.</param>
        /// <param name="ip">The port of the remote host.</param>
        public SocketStatus Connect(string hostname, ushort port)
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
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="ipEndPoint">The IPEndpoint of the remote host.</param>
        public SocketStatus Connect(IPEndPoint ipEndPoint)
        {
            var nanoAddress = Address.CreateFromIpPort(ipEndPoint.Address.ToString(), (ushort)ipEndPoint.Port);
            return Connect(nanoAddress);
        }

        /// <summary>
        /// Binds the <see cref="UdpSocket"/> to a specified port.
        /// </summary>
        /// <param name="port">The port to bind the <see cref="NanoSocket"/> to.</param>
        public SocketStatus Bind(ushort port)
        {
            ThrowIfDisposed();
            ThrowIfAlreadyBound();

            Address tempEP;

            if (m_family == AddressFamily.InterNetwork)
                tempEP = Address.Any;
            else
                tempEP = Address.IPv6Any;

            tempEP.Port = port;

            var status = (SocketStatus)NanoSocketAPI.Bind(m_socketHandle, ref tempEP);
            m_isBound = status == SocketStatus.OK;

            return status;
        }

        /// <summary>
        /// Binds the <see cref="NanoSocket"/> to a specified endpoint.
        /// </summary>
        public SocketStatus Bind(IPEndPoint localEP)
        {
            ThrowIfDisposed();
            ThrowIfAlreadyBound();

            if (localEP == null)
                throw new ArgumentNullException(nameof(localEP));

            if (localEP.AddressFamily != m_family)
                throw new ArgumentException($"{nameof(localEP)} AddressFamily is not compatible with Sockets' AddressFamily.");

            var nanoAddress = new Address(localEP);
            var status = (SocketStatus)NanoSocketAPI.Bind(m_socketHandle, ref nanoAddress);
            m_isBound = status == SocketStatus.OK;

            return status;
        }

        ///// <summary>
        ///// Sends data over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The payload to send.</param>
        ///// <param name="size">The size of the payload.</param>
        ///// <param name="bytesSent">The amount of bytes that have been sent.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(IntPtr data, int size, out int bytesSent, IPEndPoint remoteEP = null)
        //{
        //    if (data == IntPtr.Zero)
        //        ExceptionHelper.ThrowNoData();

        //    bytesSent = 0;
        //    return InnerSend((void*)data, size, ref bytesSent, remoteEP);
        //}

        ///// <summary>
        ///// Sends data over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The payload to send.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(byte[] data, IPEndPoint remoteEP = null)
        //{
        //    if (data == null) ExceptionHelper.ThrowNoData();
        //    return InnerSend(data, data.Length, 0, out _, remoteEP);
        //}

        ///// <summary>
        ///// Sends data over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The payload to send.</param>
        ///// <param name="bytesSent">The amount of bytes that have been sent.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(byte[] data, out int bytesSent, IPEndPoint remoteEP = null)
        //{
        //    if (data == null) ExceptionHelper.ThrowNoData();
        //    return InnerSend(data, data.Length, 0, out bytesSent, remoteEP);
        //}

        ///// <summary>
        ///// Sends data over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The payload to send.</param>
        ///// <param name="length">The amount of data to send.</param>
        ///// <param name="bytesSent">The amount of bytes that have been sent.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(byte[] data, int length, out int bytesSent, IPEndPoint remoteEP = null)
        //{
        //    if (data == null)
        //        ExceptionHelper.ThrowNoData();

        //    if ((uint)length > data.Length)
        //        ExceptionHelper.ThrowArgumentOutOfRange(nameof(data));

        //    return InnerSend(data, length, 0, out bytesSent, remoteEP);
        //}

        ///// <summary>
        ///// Sends data over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The payload to send.</param>
        ///// <param name="length">The amount of data to sent.</param>
        ///// <param name="offset">The offset at which to start sending.</param>
        ///// <param name="bytesSent">The amount of bytes that have been sent.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(byte[] data, int length, int offset, out int bytesSent, IPEndPoint remoteEP = null)
        //{
        //    if (data == null)
        //        ExceptionHelper.ThrowNoData();

        //    if ((uint)(length - offset) > data.Length)
        //        ExceptionHelper.ThrowArgumentOutOfRange(nameof(data));

        //    return InnerSend(data, length, offset, out bytesSent, remoteEP);
        //}

        ///// <summary>
        ///// Sends a <see cref="RawPacket"/> over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="packet">The packet to send.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(ref RawPacket packet, IPEndPoint remoteEP = null)
        //{
        //    if (packet.Data == IntPtr.Zero)
        //        ExceptionHelper.ThrowNoData();

        //    return InnerSend((void*)packet.Data, packet.Size, ref packet.SendPosition, remoteEP);
        //}

        ///// <summary>
        ///// Sends a <see cref="NetPacket"/> over the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="packet">The packet to send.</param>
        ///// <param name="remoteEP">An System.Net.IPEndPoint that represents the host and port to which to send the datagram.</param>
        //public SocketStatus Send(ref NetPacket packet, IPEndPoint remoteEP = null)
        //{
        //    if (packet.Data == null)
        //        ExceptionHelper.ThrowNoData();

        //    return InnerSend(packet.Data, packet.Size, ref packet.SendPosition, remoteEP);
        //}


        ///// <summary>
        ///// Receives raw data from the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The buffer where the received data is copied to.</param>
        ///// <param name="size">The size of the buffer.</param>
        ///// <param name="receivedBytes">The amount of copied to the buffer.</param>
        //public SocketStatus Receive(IntPtr data, int size, out int receivedBytes, ref IPEndPoint remoteEP)
        //{
        //    receivedBytes = 0;

        //    if (data == null)
        //    {
        //        ExceptionHelper.ThrowNoData();
        //        return SocketStatus.Error;
        //    }

        //    return InnerReceive((void*)data, size, out receivedBytes, ref remoteEP);
        //}

        ///// <summary>
        ///// Receives raw data from the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The buffer where the received data is copied to.</param>
        ///// <param name="receivedBytes">The amount of copied to the buffer.</param>
        //public SocketStatus Receive(byte[] data, out int receivedBytes, ref IPEndPoint remoteEP)
        //{
        //    return Receive(data, data.Length, 0, out receivedBytes, ref remoteEP);
        //}

        ///// <summary>
        ///// Receives raw data from the <see cref="UdpSocket"/>.
        ///// </summary>
        ///// <param name="data">The buffer where the received data is copied to.</param>
        ///// <param name="size">The amount of bytes to copy.</param>
        ///// <param name="receivedBytes">The amount of copied to the buffer.</param>
        //public SocketStatus Receive(byte[] data, int size, int offset, out int receivedBytes, ref IPEndPoint remoteEP)
        //{
        //    if (data == null)
        //        ExceptionHelper.ThrowArgumentNull(nameof(data));

        //    if ((uint)(size - offset) > data.Length)
        //        ExceptionHelper.ThrowArgumentOutOfRange(nameof(data));

        //    return InnerReceive(data, size, offset, out receivedBytes, ref remoteEP);
        //}

        ///// <summary>
        ///// Copies received data into the supplied NetPacket.
        ///// Must be disposed after use.
        ///// </summary>
        ///// <param name="packet">Packet to copy the data into.</param>
        //public SocketStatus Receive(ref NetPacket packet, ref IPEndPoint remoteEP)
        //{
        //    InnerReceive(m_buffer, MaxDatagramSize, 0, out int receivedBytes, ref remoteEP);

        //    if (receivedBytes > 0)
        //    {
        //        fixed (byte* buf = m_buffer)
        //        {
        //            packet.OnReceive(buf, receivedBytes);
        //        }
        //    }

        //    return SocketStatus.Done;
        //}

        ///// <summary>
        ///// Receives a <see cref="RawPacket"/> from the <see cref="UdpSocket"/>.
        ///// Must be disposed after use.
        ///// </summary>
        ///// <param name="packet">Packet that contains unmanaged memory as its data.</param>
        //public SocketStatus Receive(out RawPacket packet, ref IPEndPoint remoteEP)
        //{
        //    InnerReceive(m_buffer, MaxDatagramSize, 0, out int receivedBytes, ref remoteEP);

        //    if (receivedBytes == 0)
        //    {
        //        packet = default;
        //    }
        //    else
        //    {
        //        IntPtr packetDat = Memory.Alloc(receivedBytes);
        //        Memory.MemCpy(m_buffer, 0, (void*)packetDat, receivedBytes);

        //        packet = new RawPacket(packetDat, receivedBytes);
        //    }

        //    return SocketStatus.Done;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private SocketStatus InnerReceive(void* data, int size, out int receivedBytes, ref IPEndPoint remoteEP)
        //{
        //    InnerReceive(m_buffer, size, 0, out receivedBytes, ref remoteEP);
        //    Memory.MemCpy(m_buffer, 0, data, size);

        //    return SocketStatus.Done;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private SocketStatus InnerReceive(byte[] data, int size, int offset, out int receivedBytes, ref IPEndPoint remoteEP)
        //{
        //    EndPoint endpoint;

        //    if (m_family == AddressFamily.InterNetwork)
        //        endpoint = IpEndpointStatics.Any;
        //    else
        //        endpoint = IpEndpointStatics.IPv6Any;

        //    receivedBytes = m_socket.ReceiveFrom(data, offset, size, SocketFlags.None, ref endpoint);
        //    remoteEP = (IPEndPoint)endpoint;

        //    return SocketStatus.Done;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private SocketStatus InnerSend(byte[] data, int length, int offset, out int bytesSent, IPEndPoint endpoint = null)
        //{
        //    if (m_isActive && endpoint != null)
        //        ExceptionHelper.ThrowAlreadyActive();

        //    if (endpoint == null)
        //        bytesSent = m_socket.Send(data, offset, length, SocketFlags.None);
        //    else
        //        bytesSent = m_socket.SendTo(data, offset, length, SocketFlags.None, endpoint);

        //    return SocketStatus.Done;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private SocketStatus InnerSend(void* data, int packetSize, ref int bytesSent, IPEndPoint endpoint = null)
        //{
        //    if ((uint)packetSize > MaxDatagramSize)
        //        ExceptionHelper.ThrowPacketSizeExceeded();

        //    // Copy memory to managed buffer.
        //    Memory.MemCpy(data, m_buffer, 0, packetSize);
        //    return InnerSend(m_buffer, packetSize, 0, out bytesSent, endpoint);
        //}

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

        public void Dispose()
            => Dispose(true);

        ~NanoSocket()
            => Dispose(false);

        ///// <summary>
        ///// Gets or sets a value that specifies the Time to Live (TTL) value of Internet
        ///// Protocol (IP) packets sent by the <see cref="UdpSocket"/>.
        ///// </summary>
        //public short Ttl
        //{
        //    get { return m_socket.Ttl; }
        //    set { m_socket.Ttl = value; }
        //}
        ///// <summary>
        ///// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="UdpSocket"/>
        ///// allows Internet Protocol (IP) datagrams to be fragmented.
        ///// </summary>
        //public bool DontFragment
        //{
        //    get { return m_socket.DontFragment; }
        //    set { m_socket.DontFragment = value; }
        //}
        ///// <summary>
        ///// Gets or sets a <see cref="bool"/> value that specifies whether outgoing multicast
        ///// packets are delivered to the sending application.
        ///// </summary>
        //public bool MulticastLoopback
        //{
        //    get { return m_socket.MulticastLoopback; }
        //    set { m_socket.MulticastLoopback = value; }
        //}
        ///// <summary>
        ///// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="UdpSocket"/>
        ///// may send or receive broadcast packets.
        ///// </summary>
        //public bool EnableBroadcast
        //{
        //    get { return m_socket.EnableBroadcast; }
        //    set { m_socket.EnableBroadcast = value; }
        //}
        ///// <summary>
        ///// Gets or sets a <see cref="bool"/> value that specifies whether the <see cref="UdpSocket"/>
        ///// allows only one client to use a port.
        ///// </summary>
        //public bool ExclusiveAddressUse
        //{
        //    get { return m_socket.ExclusiveAddressUse; }
        //    set { m_socket.ExclusiveAddressUse = value; }
        //}
        ///// <summary>
        ///// Enables or disables Network Address Translation (NAT) traversal on a <see cref="UdpSocket"/>
        ///// instance.
        ///// </summary>
        //public void AllowNatTraversal(bool allowed)
        //{
        //    m_socket.SetIPProtectionLevel(allowed ? IPProtectionLevel.Unrestricted : IPProtectionLevel.EdgeRestricted);
        //}

        private void CreateNewSocketIfNeeded()
        {
            // Don't allow recreation of the socket when this object is disposed.
            if (m_isCleanedUp)
                throw new ObjectDisposedException(GetType().Name);

            if (m_socketHandle.IsCreated)
                return;

            m_socketHandle = NanoSocketAPI.Create(m_sendBufferSize, m_recvBufferSize);

            // TODO:
            //m_socket = new Socket(m_family, SocketType.Dgram, ProtocolType.Udp)
            //{
            //    Blocking = false,
            //    SendBufferSize = ushort.MaxValue,
            //    EnableBroadcast = true
            //};
        }

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
    }
}
