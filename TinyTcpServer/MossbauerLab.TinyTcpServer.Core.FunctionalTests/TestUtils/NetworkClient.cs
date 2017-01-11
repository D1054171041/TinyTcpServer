﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MossbauerLab.TinyTcpServer.Core.FunctionalTests.TestUtils
{
    internal class NetworkClient : IDisposable
    {
        public NetworkClient(EndPoint endPoint, Boolean isAsynchronous, Int32 connectionWaitTimeout = DefaultConnectionWaitTimeout,
                      Int32 readTimeout = DefaultReadTimeout, Int32 writeTimeout = DefaultWriteTimeout)
        {
            if(endPoint == null)
                throw new ArgumentNullException("endPoint");
            _endPoint = endPoint;
            _isAsynchronous = isAsynchronous;
            _connectionWaitTimeout = connectionWaitTimeout;
            _readTimeout = readTimeout;
            _writeTimeout = writeTimeout;
        }

        public Boolean Open()
        {
            try
            {
                CreateSocket();
                if (_clientSocket.Connected)
                {
                    State = _clientSocket.Connected;
                    return State;
                }
                if (_isAsynchronous)
                {
                    Boolean result = OpenAsync();
                    State = _clientSocket.Connected;
                    return result;
                }
                _clientSocket.Connect(_endPoint);
                State = _clientSocket.Connected;
                return State;
            }
            catch (Exception)
            {
                State = false;
                return false;
            }
        }

        public void Close()
        {
            if (_clientSocket.Connected)
            {
                if (_isAsynchronous)
                    CloseAsync();
                else _clientSocket.Close(_connectionWaitTimeout);
            }
            _waitCompleted.Reset();
            _readCompleted.Reset();
            _writeCompleted.Reset();
            _clientSocket.Dispose();
            State = false;
        }

        public Boolean Read(Byte[] data, out Int32 bytesRead)
        {
            try
            {
                if (_isAsynchronous)
                    return ReadAsync(data, out bytesRead);
                return ReadSync(data, out bytesRead);
            }
            catch (Exception e)
            {
                bytesRead = 0;
                Console.WriteLine("[CLIENT, Read] exception caught" + e.Message);
                return false;
            }
        }

        public Boolean Write(Byte[] data)
        {
            try
            {
                if (_isAsynchronous)
                    return WriteAsync(data);
                Int32 bytesSend = _clientSocket.Send(data, SocketFlags.Partial);
                return bytesSend == data.Length;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _waitCompleted.Dispose();
            _readCompleted.Dispose();
            _writeCompleted.Dispose();
            if (_clientSocket != null)
                _clientSocket.Dispose();
        }

        private Boolean OpenAsync()
        {
            _waitCompleted.Reset();
            _clientSocket.BeginConnect(_endPoint, OpenAsyncCallback, _clientSocket);
            _waitCompleted.Wait(_connectionWaitTimeout);
            return _clientSocket.Connected;
        }

        private void CloseAsync()
        {
            _waitCompleted.Reset();
            _clientSocket.BeginDisconnect(true, CloseAsyncCallback, _clientSocket);
            _waitCompleted.Wait(_connectionWaitTimeout);
            State = _clientSocket.Connected;
        }

        private void OpenAsyncCallback(IAsyncResult result)
        {
            ConnectAsyncCallbackImpl(result, true);
            //_clientSocket.EndConnect);
        }

        private void CloseAsyncCallback(IAsyncResult result)
        {
            ConnectAsyncCallbackImpl(result, false);
            //_clientSocket.EndDisconnect);
        }

        private void ConnectAsyncCallbackImpl(IAsyncResult result, Boolean isConnect)
        {
            Socket client = (result.AsyncState as Socket);
            if (client == null)
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException("client");
            if(isConnect)
                client.EndConnect(result);
            else client.EndDisconnect(result);
                //action(result);
            _waitCompleted.Set();
        }

        public Boolean ReadSync(Byte[] data, out Int32 bytesRead)
        {
            Boolean result = false;
            Console.WriteLine("[CLIENT, ReadSync] client read started");
            bytesRead = 0;
            for (Int32 attemp = 0; attemp < 8; attemp++)
            {
                try
                {
                    bytesRead = _clientSocket.Receive(data, SocketFlags.None);
                    if (bytesRead > 0)
                    {
                        result = true;
                        break;
                    }
                }
                catch (Exception){ }
            }
            Console.WriteLine("[CLIENT, ReadSync] client read done");
            return result;
        }

        private Boolean ReadAsync(Byte[] data, out Int32 bytesRead)
        {
            //todo: umv make more complicated error handling
            try
            {
                Console.WriteLine("[CLIENT, ReadAsync] client read started");
                _bytesRead = 0;

                for (Int32 attempt = 0; attempt < 64; attempt++)
                {
                    if (_clientSocket.Available == 0)
                    {
                        _clientSocket.Poll(50000, SelectMode.SelectRead);
                        continue;
                    }
                    attempt = 0;
                    _readCompleted.Reset();
                    Int32 offset = _bytesRead;
                    Int32 size = _clientSocket.Available;
                    lock (_synch)
                        _clientSocket.BeginReceive(data, offset, size, SocketFlags.Partial, ReadAsyncCallback, _clientSocket);
                    _readCompleted.Wait(10);
                    if (_bytesRead == data.Length)
                        break;
                }
                bytesRead = _bytesRead;
                Console.WriteLine("[CLIENT, ReadAsync] client read done");
                return true;
            }
            catch (Exception)
            {
                bytesRead = 0;
                Console.WriteLine("[CLIENT, ReadAsync] something goes wrong");
                return false;
            }
        }

        private void ReadAsyncCallback(IAsyncResult result)
        {
            Socket client = (result.AsyncState as Socket);
            if (client == null)
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException("client");
            _bytesRead += client.EndReceive(result);
            _readCompleted.Set(); 
        }

        private Boolean WriteAsync(Byte[] data)
        {
            //todo: umv make more complicated error handling
            Console.WriteLine("[Client, WriteAsync] write started");
            try
            {
                if (!State)
                    return false;
                _bytesSend = 0;
                while (_bytesSend < data.Length)
                {
                    _writeCompleted.Reset();
                    //Int32 offset = _bytesSend;
                    //Int32 size = Math.Min(MaximumPacketSize, data.Length - _bytesSend);
/*                    lock (_synch)
                    {
                        _clientSocket.BeginSend(data, offset, size, SocketFlags.Partial, WriteAsyncCallback,  _clientSocket);
                    }*/
                    lock (_synch)
                        _clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None, WriteAsyncCallback, _clientSocket);
                    _writeCompleted.Wait(_writeTimeout);
                }
                Console.WriteLine("[Client, WriteAsync] write done, bytes written: " + _bytesSend);
                return _bytesSend == data.Length;
            }
            catch (Exception)
            {
                Console.WriteLine("[Client, WriteAsync] something goes wrong");
                return false;
            }
        }

        private void WriteAsyncCallback(IAsyncResult result)
        {
            Socket client = (result.AsyncState as Socket);
            if (client == null)
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException("client");
            _bytesSend += client.EndSend(result);
            _writeCompleted.Set();
        }

        private void CreateSocket()
        {
            _clientSocket = new Socket(DeviceAddressFamily, DeviceSocketType, DeviceProtocolType);
            _clientSocket.SendTimeout = _writeTimeout;
            _clientSocket.ReceiveTimeout = _readTimeout;
        }

        public Boolean State { get; private set; }

        private const Int32 MaximumPacketSize = 1536;
        private const Int32 DefaultConnectionWaitTimeout = 4000;
        private const Int32 DefaultReadTimeout = 2000;
        private const Int32 DefaultWriteTimeout = 2000;
        private const AddressFamily DeviceAddressFamily = AddressFamily.InterNetwork;
        private const SocketType DeviceSocketType = SocketType.Stream;
        private const ProtocolType DeviceProtocolType = ProtocolType.Tcp;
        //private const Int32 NumberOfRetries = 10;

        private Socket _clientSocket;
        private readonly EndPoint _endPoint;
        private readonly Boolean _isAsynchronous;
        private readonly Int32 _connectionWaitTimeout;
        private readonly Int32 _readTimeout;
        private readonly Int32 _writeTimeout;
        private readonly ManualResetEventSlim _waitCompleted = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _readCompleted = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _writeCompleted = new ManualResetEventSlim(false);
        private readonly Object _synch = new Object();
        private Int32 _bytesRead;
        private Int32 _bytesSend;
    }
}