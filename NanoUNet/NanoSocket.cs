﻿using NanoSockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NanoUNet
{
    public sealed class NanoSocket : IDisposable
    {
        //public const int MaxDatagramSize = 65507;

        private const int DefaultSendBufSize = 65536;
        private const int DefaultRecvBufSize = 65536;

#pragma warning disable IDE0032, IDE0044
        private NanoSockets.Socket m_socketHandle;
        private Address m_rightEndPoint;
        private AddressFamily m_addressFamily;

        private bool m_isConnected = false;
        private bool m_isBound = false;
        private bool m_isCleanedUp = false;
        private bool m_isBlocking = true;
#pragma warning restore IDE0032, IDE0044

        static NanoSocket()
        {
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
            NanoSocketAPI.Initialize();
        }

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
        /// Gets a value that indicates whether the <see cref="NanoSocket"/> is in blocking mode.
        /// </summary>
        public bool Blocking
            => m_isBlocking;
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

            m_socketHandle = NanoSocketAPI.Create(DefaultSendBufSize, DefaultRecvBufSize);
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
        /// Sets the <see cref="NanoSocket"/> to blocking mode.
        /// </summary>
        public void SetNonBlocking()
        {
            if (NanoSocketAPI.SetNonBlocking(m_socketHandle) != SocketStatus.OK)
                throw new InvalidOperationException(ErrorCodes.SetSocketOptionError);

            m_isBlocking = false;
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
            public const string GetSocketOptionError = "GetSocketOption returned an error.";
            public const string SetSocketOptionError = "SetSocketOption returned an error.";
        }
    }
}
