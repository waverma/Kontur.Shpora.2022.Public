using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cluster;
using ClusterClient.Clients;
using FluentAssertions;
using NUnit.Framework;

namespace ClusterTests
{
	public class SmartClusterClientTest : ClusterTest
	{
		protected override ClusterClientBase CreateClient(string[] replicaAddresses)
			=> new SmartClusterClient(replicaAddresses);

		[Test]
		public void ShouldReturnSuccessWhenLastReplicaIsGoodAndOthersAreSlow()
		{
			for (int i = 0; i < 3; i++)
				CreateServer(Slow);
			CreateServer(Fast);

			ProcessRequests(Timeout).Last().Should().BeCloseTo(TimeSpan.FromMilliseconds(3 * Timeout / 4 + Fast), Epsilon);
		}

		[Test]
		public void ShouldReturnSuccessWhenLastReplicaIsGoodAndOthersAreBad()
		{
			for (int i = 0; i < 3; i++)
				CreateServer(1, status: 500);
			CreateServer(Fast);

			ProcessRequests(Timeout).Last().Should().BeCloseTo(TimeSpan.FromMilliseconds(Fast), Epsilon);
		}

		[Test]
		public void ShouldThrowAfterTimeout()
		{
			for (var i = 0; i < 10; i++)
				CreateServer(Slow);

			var sw = Stopwatch.StartNew();
			Assert.Throws<TimeoutException>(() => ProcessRequests(Timeout));
			sw.Elapsed.Should().BeCloseTo(TimeSpan.FromMilliseconds(Timeout), Epsilon);
		}

		[Test]
		public void ShouldNotForgetPreviousAttemptWhenStartNew()
		{
			CreateServer(4500);
			CreateServer(3000);
			CreateServer(10000);

			foreach(var time in ProcessRequests(6000))
				time.Should().BeCloseTo(TimeSpan.FromMilliseconds(4500), Epsilon);
		}

		[Test]
		public void ShouldNotSpendTimeOnBad()
		{
			CreateServer(1, status: 500);
			CreateServer(1, status: 500);
			CreateServer(4000);
			CreateServer(10000);

			foreach(var time in ProcessRequests(6000))
				time.Should().BeCloseTo(TimeSpan.FromMilliseconds(4000), Epsilon);
		}
		
		[Test]
		public void Client_should_Recalculating()
		{
			CreateServer(1300);
			CreateServer(1300);
			CreateServer(1300);
			CreateServer(1300);
			CreateServer(1300);
			CreateServer(300);
		
			var addresses = clusterServers
				.Select(cs => $"http://127.0.0.1:{cs.ServerOptions.Port}/{cs.ServerOptions.MethodName}/")
				.ToArray();

			var client = CreateClient(addresses);

			for (int i = 0; i < 20; i++)
			{
				ProcessRequests___(1299, client);
			}
		}
		
		[Test]
		public void Client_should_Recalculating2()
		{
			CreateServer(2000);
			CreateServer(Fastest);
		
			var addresses = clusterServers
				.Select(cs => $"http://127.0.0.1:{cs.ServerOptions.Port}/{cs.ServerOptions.MethodName}/")
				.ToArray();

			var client = CreateClient(addresses);
			
			for (int i = 0; i < 1000; i++)
			{
				ProcessRequests___(2100, client);
			}
			
		}
		
		protected void ProcessRequests___(double timeout, ClusterClientBase client)
		{
			var timer = Stopwatch.StartNew();
			var query = 0.ToString("x8");
			try
			{
				var clientResult = client.ProcessRequestAsync(query, TimeSpan.FromMilliseconds(timeout)).Result;
				timer.Stop();

				clientResult.Should().Be(Encoding.UTF8.GetString(ClusterHelpers.GetBase64HashBytes(query)));
				timer.ElapsedMilliseconds.Should().BeLessThan((long)timeout + Epsilon);

				Console.WriteLine("Query \"{0}\" successful ({1} ms)", query, timer.ElapsedMilliseconds);

			}
			catch(Exception)
			{
				Console.WriteLine("Query \"{0}\" timeout ({1} ms)", query, timer.ElapsedMilliseconds);
			}
		}
	}
}