﻿using System;
using MossbauerLab.TinyTcpServer.Core.Client;
using MossbauerLab.TinyTcpServer.Core.Handlers;
using MossbauerLab.TinyTcpServer.Core.Server;

namespace Experimental
{
    public class EchoServerScript
    {
        public void Init(ref ITcpServer server)
        {
            if (server == null)
                throw new NullReferenceException("server");
            _server = server;
            _connectHandlerId = Guid.NewGuid();
            _dataHandlerId = Guid.NewGuid();
            //Console.WriteLine("Init....");
            _server.AddConnectionHandler(_connectHandlerId, OnClientConnection);
            _server.AddHandler(new TcpClientHandlerInfo(_dataHandlerId), OnClientExchange);
        }

        public Byte[] OnClientExchange(Byte[] receivedData, TcpClientHandlerInfo info)
        {
            lock (receivedData)
            {
                Byte[] outputData = new Byte[receivedData.Length];
                Array.Copy(receivedData, outputData, receivedData.Length);
                return outputData;
            }
        }

        public void OnClientConnection(TcpClientContext context, Boolean connect)  // connect true if client connected and false if disconnected
        {

        }

        private ITcpServer _server;
        private Guid _connectHandlerId;
        private Guid _dataHandlerId;
    }
}
