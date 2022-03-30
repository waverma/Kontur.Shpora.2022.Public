﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NMAP;

namespace HackChat
{
	public class Chat
	{
		public const int DefaultPort = 31337;

		private readonly byte[] PingMsg = new byte[1];
		private readonly ConcurrentDictionary<IPEndPoint, (TcpClient Client, NetworkStream Stream)> OutboundConnections = new();

		private readonly int port;
		private readonly TcpListener tcpListener;

		public Chat(int port) => tcpListener = new TcpListener(IPAddress.Any, this.port = port);

		public void Start()
		{
            tcpListener.Start(100500);
			Task.Factory.StartNew(() =>
			{
				while(true)
				{
					var tcpClient = tcpListener.AcceptTcpClient();
					ConsoleWriteLineAsync($"[{tcpClient.Client.RemoteEndPoint}] connected", ConsoleColor.Yellow);
					Task.Run(() => ProcessInboundConnectionsAsync(tcpClient));
				}
			}, TaskCreationOptions.LongRunning);

			Task.Factory.StartNew(DiscoverLoop, TaskCreationOptions.LongRunning);

			Task.Factory.StartNew(() =>
			{
				string line;
				while ((line = Console.ReadLine()) != null)
					Task.Run(() => BroadcastAsync((string)line.Clone()));
			}, TaskCreationOptions.LongRunning);
		}

        private async Task ProcessInboundConnectionsAsync(TcpClient tcpClient)
        {
            EndPoint endpoint = null;
            try { endpoint = tcpClient.Client.RemoteEndPoint; } catch { /* ignored */ }

            try
            {
                using (tcpClient)
                {
                    var stream = tcpClient.GetStream();
                    await ReadLinesToConsoleAsync(stream);
                }
            }
            catch { /* ignored */ }
            await ConsoleWriteLineAsync($"[{endpoint}] disconnected", ConsoleColor.DarkRed);
        }

        private async Task ReadLinesToConsoleAsync(Stream stream)
        {
            string line;
            using var sr = new StreamReader(stream);
            while ((line = await sr.ReadLineAsync()) != null)
                await ConsoleWriteLineAsync($"[{((NetworkStream)stream).Socket.RemoteEndPoint}] {line}");
        }


        private async void DiscoverLoop()
		{
			while(true)
			{
				try { await Discover(); } catch { /* ignored */ }
				await Task.Delay(1000);
			}
		}

        private async Task Discover()
		{
			OutboundConnections.Where(pair => !pair.Value.Client.Client.Connected).ForEach(pair =>
			{
				try { pair.Value.Client.Dispose(); } catch { /* ignored */ }
				ConsoleWriteLineAsync($"[ME] disconnected from {pair.Key}", ConsoleColor.DarkRed).Wait();
				OutboundConnections.TryRemove(pair);
			});

            var myAddresses = await GetMyAddresses();
			var nearbyAddresses = await GetNearbyAddresses(myAddresses);

			await Task.WhenAll(nearbyAddresses.Select(x => CheckPortAndAddConnection(x)));
        }
        
        private async Task CheckPortAndAddConnection(IPAddress ipAddr, int port=31337, int timeout = 3000)
        {
	        using var tcpClient = new TcpClient();

	        var connectTask = tcpClient.ConnectAsync(ipAddr, port);
	        await Task.WhenAny(connectTask, Task.Delay(timeout));

	        if (connectTask.Status == TaskStatus.RanToCompletion)
	        {
		        await connectTask;
		        var id = IPEndPoint.Parse($"{ipAddr}:{port}");
		        Console.WriteLine(id);
		        OutboundConnections[id] = (tcpClient, tcpClient.GetStream());
	        }
        }

        private async Task<IEnumerable<IPAddress>> GetNearbyAddresses(IPAddress[] myAddresses)
        {
            return myAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .SelectMany(CalcNearbyIPAddresses);
        }

        private static async Task<IPAddress[]> GetMyAddresses()
        {
            return (await Dns.GetHostEntryAsync(Dns.GetHostName())).AddressList;
        }

        private IEnumerable<IPAddress> CalcNearbyIPAddresses(IPAddress ip)
        {
	        // yield return IPAddress.Parse("127.0.0.1");
	        // yield break;
	        var bytes = ip.GetAddressBytes();
	        Array.Reverse(bytes);
	        var raw = BitConverter.ToUInt32(bytes, 0);
	        for (uint b = 1; b <= 1022; b++)
	        {
		        var newIp = BitConverter.GetBytes((raw & 0b11111111_11111111_11111100_00000000) | b);
		        Array.Reverse(newIp);
		        yield return new IPAddress(newIp);
	        }
        }

        

        private async Task BroadcastAsync(string message)
        {
	        await Task.WhenAll(OutboundConnections.Select(x => x.Value.Stream.WriteAsync(message.Select(y => byte.Parse(y.ToString())).ToArray(), 0, message.Length)));
        }


        private SemaphoreSlim consoleSemaphore = new SemaphoreSlim(1, 1);
        private async Task ConsoleWriteLineAsync(string str, ConsoleColor color = ConsoleColor.Gray)
        {
            await consoleSemaphore.WaitAsync();
            try
            {
                Console.ForegroundColor = color;
                await Console.Out.WriteLineAsync(str);
                Console.ResetColor();
            }
            finally
            {
                consoleSemaphore.Release();
            }
        }
	}
}