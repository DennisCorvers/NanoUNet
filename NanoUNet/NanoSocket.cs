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
        /// Helper to ensure calls to WSAStartup / WSACleanup
        /// </summary>
        private static readonly LibInitializer libInit = new LibInitializer();

        public const int MaxDatagramSize = 65507;

#pragma warning disable IDE0032, IDE0044
        private NanoSockets.Socket m_socketHandle;
        private Address m_rightEndPoint;
        private AddressFamily m_addressFamily;

        private bool m_isConnected = false;
        private bool m_isBound = false;
        private bool m_isCleanedUp = false;
        private bool m_isBlocking = true;
#pragma warning restore IDE0032, IDE0044

        /// <summary>
        /// Gets the local endpoint.
        /// </summary>
        public Address LocalEndPoint
        {
            get
            {
                ThrowIfDisposed();

                Address localEP = default(Address);
                if (NanoSocketAPI.GetAddress(m_socketHandle, ref localEP) != SocketStatus.OK)
                    return default(Address);

                return localEP;
            }
        }
        /// <summary>
        /// Gets the remote endpoint.
        /// </summary>
        public Address RemoteEndPoint
        {
            get
            {
                ThrowIfDisposed();

                if (!m_isConnected)
                    return default(Address);

                return m_rightEndPoint;
            }
        }
        /// <summary>
        /// Gets or sets a bool that indicates whether the <see cref="NanoSocket"/> is in blocking mode.
        /// </summary>
        public bool Blocking
        {
            get
            {
                return m_isBlocking;
            }
            set
            {
                NanoSocketAPI.SetNonBlocking(m_socketHandle, value);
                m_isBlocking = value;
            }
        }
        /// <summary>
        /// Indicates if the <see cref="NanoSocket"/> is connected.
        /// </summary>
        public bool Connected
            => m_isConnected;
        /// <summary>
        /// Gets the protocol type of the <see cref="NanoSocket"/>.
        /// </summary>
        public ProtocolType ProtocolType
            => ProtocolType.Udp;
        /// <summary>
        /// Indicates if the <see cref="NanoSocket"/> is bound to a port.
        /// </summary>
        public bool IsBound
            => m_isBound;
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
        /// Gets or sets a value that specifies the size of the receive buffer of the <see cref="NanoSocket"/>.
        /// </summary>
        public int ReceiveBufferSize
        {
            get
            {
                return GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer);
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
            }
        }
        /// <summary>
        /// Gets or sets a value that specifies the size of the send buffer of the <see cref="NanoSocket"/>.
        /// </summary>
        public int SendBufferSize
        {
            get
            {
                return GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer);
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
            }
        }
        /// <summary>
        /// Gets or sets a value that specifies the amount of time after which a synchronous
        /// <see cref="NanoSocket"/>.Receive call will time out.
        /// </summary>
        public int ReceiveTimeout
        {
            get
            {
                return GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout);
            }
            set
            {
                if (value < 0)
                    value = 0;

                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
            }
        }
        /// <summary>
        /// Gets or sets a value that specifies the amount of time after which a synchronous
        /// <see cref="NanoSocket"/>.Send call will time out.
        /// </summary>
        public int SendTimeout
        {
            get
            {
                return GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout);
            }
            set
            {
                if (value < 0)
                    value = 0;

                SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
            }
        }
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
        /// Gets the type of the <see cref="NanoSocket"/>.
        /// </summary>
        public SocketType SocketType
            => SocketType.Dgram;
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
        /// Creates a new <see cref="NanoSocket"/>.
        /// </summary>
        public NanoSocket(AddressFamily family = AddressFamily.InterNetwork)
        {
            ValidateAddressFamily(family);

            m_addressFamily = family;

            m_socketHandle = NanoSocketAPI.Create(1024 * 64, 1024 * 64);
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
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));

            if (sendBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));

            m_addressFamily = family;

            m_socketHandle = NanoSocketAPI.Create(receiveBufferSize, sendBufferSize);
        }

        ~NanoSocket()
            => Dispose(false);

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
        /// Disposes the <see cref="NanoSocket"/> and frees the underlying resources.
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Establishes a connection to the remote host.
        /// </summary>
        /// <param name="address">The Ip Address of the remote host.</param>
        /// <param name="port">The port of the remote host.</param>
        public SocketError Connect(Address address)
        {
            ThrowIfDisposed();

            var status = (SocketError)NanoSocketAPI.Connect(m_socketHandle, ref address);

            if (status == SocketError.Success)
            {
                m_isConnected = true;
                m_rightEndPoint = address;
            }

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
        /// Releases all resources used by the current instance of the <see cref="NanoSocket"/> class.
        /// </summary>
        public void Dispose()
            => Dispose(true);

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
                throw new InvalidOperationException("GetSocketOption returned an error.");

            return tempValue;
        }

        /// <summary>
        /// Determines the status of the <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="milliseconds">The time to wait for a response, in milliseconds.</param>
        public bool Poll(int milliseconds)
        {
            ThrowIfDisposed();

            return NanoSocketAPI.Poll(m_socketHandle, milliseconds) > 0;
        }

        /// <summary>
        /// Receives data from a bound <see cref="NanoSocket"/> into a receive buffer.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        public int Receive(byte[] buffer)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer);

            return NanoSocketAPI.Receive(m_socketHandle, IntPtr.Zero, buffer, buffer.Length);
        }
        /// <summary>
        /// Receives data from a bound <see cref="NanoSocket"/> into a receive buffer.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        public int Receive(byte[] buffer, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Receive(m_socketHandle, IntPtr.Zero, buffer, size);
        }
        /// <summary>
        /// Receives data from a bound <see cref="NanoSocket"/> into a receive buffer.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        /// <param name="offset">The location in buffer to store the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        public int Receive(byte[] buffer, int offset, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, offset, size);

            return NanoSocketAPI.Receive(m_socketHandle, IntPtr.Zero, buffer, offset, size);
        }
        /// <summary>
        /// Receives data from a bound <see cref="NanoSocket"/> into a receive buffer.
        /// </summary>
        /// <param name="buffer">An <see cref="IntPtr"/> that is the storage location for the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        public int Receive(IntPtr buffer, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Receive(m_socketHandle, IntPtr.Zero, buffer, size);
        }

        /// <summary>
        /// Receives a datagram into the data buffer and stores the endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        /// <param name="address">An <see cref="Address"/>, passed by reference, that represents the address of the sender.</param>
        public int ReceiveFrom(byte[] buffer, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ThrowIfNotBound();
            ValidateBufferArguments(buffer);

            return NanoSocketAPI.Receive(m_socketHandle, ref remoteEP, buffer, buffer.Length);
        }
        /// <summary>
        /// Receives a datagram into the data buffer and stores the endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="address">An <see cref="Address"/>, passed by reference, that represents the address of the sender.</param>
        /// <returns></returns>
        public int ReceiveFrom(byte[] buffer, int size, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ThrowIfNotBound();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Receive(m_socketHandle, ref remoteEP, buffer, size);
        }
        /// <summary>
        /// Receives a datagram into the data buffer and stores the endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="byte"/> that is the storage location for the received data.</param>
        /// <param name="offset">The location in buffer to store the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="address">An <see cref="Address"/>, passed by reference, that represents the address of the sender.</param>
        public int ReceiveFrom(byte[] buffer, int offset, int size, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ThrowIfNotBound();
            ValidateBufferArguments(buffer, offset, size);

            return NanoSocketAPI.Receive(m_socketHandle, ref remoteEP, buffer, offset, size);
        }
        /// <summary>
        /// Receives a datagram into the data buffer and stores the endpoint.
        /// </summary>
        /// <param name="buffer">An <see cref="IntPtr"/> that is the storage location for the received data.</param>
        /// <param name="size">The number of bytes to receive.</param>
        /// <param name="address">An <see cref="Address"/>, passed by reference, that represents the address of the sender.</param>
        public int ReceiveFrom(IntPtr buffer, int size, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ThrowIfNotBound();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Receive(m_socketHandle, ref remoteEP, buffer, size);
        }

        /// <summary>
        /// Sends data to a connected <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        public int Send(byte[] buffer)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer);

            return NanoSocketAPI.Send(m_socketHandle, IntPtr.Zero, buffer, buffer.Length);
        }
        /// <summary>
        /// Sends data to a connected <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        /// <param name="size">The number of bytes to send.</param>
        public int Send(byte[] buffer, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Send(m_socketHandle, IntPtr.Zero, buffer, size);
        }
        /// <summary>
        /// Sends data to a connected <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data.</param>
        public int Send(byte[] buffer, int offset, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, offset, size);

            return NanoSocketAPI.Send(m_socketHandle, IntPtr.Zero, buffer, offset, size);
        }
        /// <summary>
        /// Sends data to a connected <see cref="NanoSocket"/>.
        /// </summary>
        /// <param name="buffer">An <see cref="IntPtr"/> that contains the data to be sent.</param>
        /// <param name="size">The number of bytes to send.</param>
        public int Send(IntPtr buffer, int size)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Send(m_socketHandle, IntPtr.Zero, buffer, size);
        }

        /// <summary>
        /// Sends data to the specified endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        /// <param name="remoteEP">The <see cref="Address"/> that represents the destination for the data.</param>
        public int SendTo(byte[] buffer, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ValidateBufferArguments(buffer);

            return NanoSocketAPI.Send(m_socketHandle, ref remoteEP, buffer, buffer.Length);
        }
        /// <summary>
        /// Sends data to the specified endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="remoteEP">The <see cref="Address"/> that represents the destination for the data.</param>
        public int SendTo(byte[] buffer, int size, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ValidateBufferArguments(buffer);

            return NanoSocketAPI.Send(m_socketHandle, ref remoteEP, buffer, size);
        }
        /// <summary>
        /// Sends data to the specified endpoint.
        /// </summary>
        /// <param name="buffer">An array of type <see cref="System.Byte"/> that contains the data to be sent.</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="remoteEP">The <see cref="Address"/> that represents the destination for the data.</param>
        public int SendTo(byte[] buffer, int offset, int size, ref Address remoteEP)
        {
            ThrowIfDisposed();
            ValidateBufferArguments(buffer, offset, size);

            return NanoSocketAPI.Send(m_socketHandle, ref remoteEP, buffer, offset, size);
        }
        /// <summary>
        /// Sends data to the specified endpoint.
        /// </summary>
        /// <param name="buffer">An <see cref="IntPtr"/> that contains the data to be sent.</param>
        /// <param name="size">The number of bytes to send.</param>
        /// <param name="remoteEP">The <see cref="Address"/> that represents the destination for the data.</param>
        public int SendTo(IntPtr buffer, int size, ref Address removeEP)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            ValidateBufferArguments(buffer, size);

            return NanoSocketAPI.Send(m_socketHandle, ref removeEP, buffer, size);
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
                throw new InvalidOperationException("SetSocketOption returned an error.");
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

        #region Validation
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfNotConnected()
        {
            if (!m_isConnected)
                ThrowDgramSocketNotConnected();

            void ThrowDgramSocketNotConnected() => throw new InvalidOperationException("Cannot send to, or receive from an arbitrary host while not conneceted.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfNotBound()
        {
            if (!m_isBound)
                ThrowSocketNotBound();

            void ThrowSocketNotBound() => throw new InvalidOperationException("You must call the Bind method before performing this operation.");
        }

        private void ValidateAddressFamily(AddressFamily family)
        {
            if (family != AddressFamily.InterNetwork &&
                family != AddressFamily.InterNetworkV6)
            {
                throw new ArgumentException("Invalid AddressFamily for UDP protocol.", nameof(family));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateBufferArguments(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateBufferArguments(byte[] buffer, int size)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if ((uint)size > (uint)buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(size));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateBufferArguments(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if ((uint)offset > (uint)buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if ((uint)size > (uint)(buffer.Length - offset))
                throw new ArgumentOutOfRangeException(nameof(size));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateBufferArguments(IntPtr buffer, int size)
        {
            if (buffer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(buffer));
        }
        #endregion

        private sealed class LibInitializer
        {
            public LibInitializer()
            {
                NanoSocketAPI.Initialize();
            }

            ~LibInitializer()
            {
                NanoSocketAPI.Deinitialize();
            }
        }
    }
}
