﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;

namespace NetCoreNetworkBenchmark.LiteNetLib
{
	internal class EchoClient
	{
		public bool IsConnected { get; private set; }
		public bool IsDisposed { get; private set; }

		private readonly int id;
		private readonly BenchmarkConfiguration config;
		private readonly BenchmarkData benchmarkData;

		private readonly byte[] message;
		private readonly int tickRate;
		private readonly EventBasedNetListener listener;
		private readonly NetManager netManager;
		private NetPeer peer;

		public EchoClient(int id, BenchmarkConfiguration config)
		{
			this.id = id;
			this.config = config;
			benchmarkData = config.BenchmarkData;
			message = config.Message;
			tickRate = Math.Max(1000 / this.config.TickRateClient, 1);

			listener = new EventBasedNetListener();
			netManager = new NetManager(listener);
			netManager.IPv6Enabled = IPv6Mode.Disabled;
			netManager.UnsyncedEvents = true;
			netManager.DisconnectTimeout = 10000;

			IsConnected = false;
			IsDisposed = false;

			listener.PeerConnectedEvent += OnPeerConnected;
			listener.PeerDisconnectedEvent += OnPeerDisconnected;
			listener.NetworkReceiveEvent += OnNetworkReceive;
			listener.NetworkErrorEvent += OnNetworkError;
		}

		public void Start()
		{
			netManager.Start();
			peer = netManager.Connect(config.Address, config.Port, "ConnectionKey");
			IsDisposed = false;
		}

		public void StartSendingMessages()
		{
			var parallelMessagesPerClient = config.ParallelMessagesPerClient;

			for (int i = 0; i < parallelMessagesPerClient; i++)
			{
				Send(message, DeliveryMethod.ReliableUnordered);
			}
			netManager.TriggerUpdate();
		}

		public Task Disconnect()
		{
			if (!IsConnected)
			{
				return Task.CompletedTask;
			}

			var clientDisconnected = Task.Factory.StartNew(() =>
			{
				peer.Disconnect();
			}, TaskCreationOptions.LongRunning);

			return clientDisconnected;
		}

		public Task Stop()
		{
			// If not disconnected, stopping consumes a lot of time
			var stopClient = Task.Factory.StartNew(() =>
			{
				netManager.Stop(false);
			}, TaskCreationOptions.LongRunning);

			return stopClient;
		}

		public void Dispose()
		{
			listener.PeerConnectedEvent -= OnPeerConnected;
			listener.PeerDisconnectedEvent -= OnPeerDisconnected;
			listener.NetworkReceiveEvent -= OnNetworkReceive;
			listener.NetworkErrorEvent -= OnNetworkError;

			IsDisposed = true;
		}

		private void Send(byte[] bytes, DeliveryMethod deliverymethod)
		{
			if (!IsConnected)
			{
				return;
			}

			peer.Send(bytes, deliverymethod);
			Interlocked.Increment(ref benchmarkData.MessagesClientSent);
		}

		private void OnPeerConnected(NetPeer peer)
		{
			IsConnected = true;
		}

		private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
		{
			if (disconnectInfo.Reason == DisconnectReason.Timeout && benchmarkData.Running)
			{
				Utilities.WriteVerboseLine($"Client {id} disconnected due to timeout. Probably the server is overwhelmed by the requests.");
				Interlocked.Increment(ref benchmarkData.Errors);
			}
			this.peer = null;
			IsConnected = false;
		}

		private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
			if (benchmarkData.Running)
			{
				Interlocked.Increment(ref benchmarkData.MessagesClientReceived);
				Send(message, deliverymethod);
				netManager.TriggerUpdate();
			}

			reader.Recycle();
		}

		private void OnNetworkError(IPEndPoint endpoint, SocketError socketerror)
		{
			if (benchmarkData.Running)
			{
				Utilities.WriteVerboseLine($"Error Client {id}: {socketerror}");
				Interlocked.Increment(ref benchmarkData.Errors);
			}
		}
	}
}
