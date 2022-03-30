using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using log4net;

namespace NMAP
{
	public class TPLScanner : SequentialScanner
	{
		protected virtual ILog log => LogManager.GetLogger(typeof(SequentialScanner));

		public override Task Scan(IPAddress[] ipAddrs, int[] ports)
		{
			return Task.Run(async () =>
			{
				await Task.WhenAll(ipAddrs.Select(x => ProcessIpAddr(ports, x)));
			});
		}

		private async Task ProcessIpAddr(int[] ports, IPAddress ipAddr)
		{
			var add = await PingAddr(ipAddr);
			
			if(add != IPStatus.Success)
				return;

			await Task.WhenAll(ports.Select(x => CheckPort(ipAddr, x)));
		}

		protected new async Task<IPStatus> PingAddr(IPAddress ipAddr, int timeout = 3000)
		{
			log.Info($"Pinging {ipAddr}");
			using var ping = new Ping();
			var reply = await ping.SendPingAsync(ipAddr, timeout);
			// await reply;
			
			var status = reply.Status;
			log.Info($"Pinged {ipAddr}: {status}");
			return status;
		}

		protected new async Task CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
		{
		 	using var tcpClient = new TcpClient();
			log.Info($"Checking {ipAddr}:{port}");

			var connectStatus = await tcpClient.ConnectWithTimeoutAsync(ipAddr, port, timeout);
			// await connectStatus;

			var portStatus = connectStatus switch
			{
				TaskStatus.RanToCompletion => PortStatus.OPEN,
				TaskStatus.Faulted => PortStatus.CLOSED,
				_ => PortStatus.FILTERED
			};
			log.Info($"Checked {ipAddr}:{port} - {portStatus}");
		}
	}
}