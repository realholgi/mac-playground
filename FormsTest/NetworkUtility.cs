using System;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
#if MAC
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace FormsTest
{
	public class NetworkUtility
	{
		private static readonly object staticLock = new object();
		private static event EventHandler<EventArgs>? networkAvailabilityChanged;
		private static bool oldIsNetworkAvailable;

#if !MAC
		private static class NativeMethods
		{
			[DllImport("wininet", SetLastError = true)]
			public extern static bool InternetGetConnectedState(out int lpdwFlags, int dwReserved);
		}

		private static bool RealIsNetworkAvailable
		{
			get
			{
				try
				{
					foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces())
					{
						if (iface.OperationalStatus == OperationalStatus.Up &&
							iface.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
							iface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
						    iface.NetworkInterfaceType != NetworkInterfaceType.Unknown) // On OS X some tunnel interfaces (eg. utun0) are reported as Unknown.
						{
							var ipProperties = iface.GetIPProperties();
							bool hasValidAddress = false;
							if (ipProperties.GatewayAddresses.Count > 0)
							{
								bool isDnsEligibleFailed = false;
								foreach (var uni in ipProperties.UnicastAddresses)
								{
									// Skip 169.254.xxx.xxx addresses
									if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
									{
										byte[] addr = uni.Address.GetAddressBytes();
										if (addr.Length == 4 && (addr[0] != 169 || addr[1] != 254))
										{
											hasValidAddress = true;
											break;
										}
									}
									else
									{
										try
										{
											if (!isDnsEligibleFailed && uni.IsDnsEligible) // throws on .net6-macos
											{
												//Logic for detecting IsDnsEligible in System.Net.NetworkInformation.SystemIPAddressInformation is incorrect, 
												// -> use it just as fallback
												hasValidAddress = true;
												break;
											}
										}
										catch
										{
											isDnsEligibleFailed = true;
										}
									}
								}

								if (isDnsEligibleFailed)
									hasValidAddress |= ipProperties.HasNonLoopbackDns();

								if (hasValidAddress)
									return true;
							}
						}
					}
				}
				catch (Exception e)
				{
					if (e is NetworkInformationException || // Something is screwed up really bad. 
						e is NullReferenceException) // Old .NET Framework
					{
						int flags;
						return NativeMethods.InternetGetConnectedState(out flags, 0);
					}
				}
				return false;
			}
		}

		static void AddNetworkChangeHandler()
		{
			NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
		}

		static void RemoveNetworkChangeHandler()
		{
			NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
		}

		static void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("NetworkChange_NetworkAddressChanged");
			bool isNetworkAvailable = RealIsNetworkAvailable;
			if (oldIsNetworkAvailable != isNetworkAvailable)
			{
				oldIsNetworkAvailable = isNetworkAvailable;
				networkAvailabilityChanged?.Invoke(sender, EventArgs.Empty);
			}
		}
#endif

		public static bool IsNetworkAvailable
		{
			get
			{
				lock (staticLock)
				{
#if !MAC
					if (networkAvailabilityChanged == null)
						return RealIsNetworkAvailable;
#endif
					return oldIsNetworkAvailable;
				}
			}
		}

		public static event EventHandler<EventArgs> NetworkAvailabilityChanged
		{
			add
			{
				lock (staticLock)
				{
					if (networkAvailabilityChanged == null)
					{
#if !MAC
						oldIsNetworkAvailable = RealIsNetworkAvailable;
#endif
						networkAvailabilityChanged += value;
						AddNetworkChangeHandler();
					}
					else
						networkAvailabilityChanged += value;
				}
			}
			remove
			{
				lock (staticLock)
				{
					networkAvailabilityChanged -= value;
					if (networkAvailabilityChanged == null)
					{
						try
						{
							RemoveNetworkChangeHandler();
						}
						catch (NullReferenceException)
						{
							//NullReferenceException bugs are reported when removing the delegate - just don't crash..
							System.Diagnostics.Debug.Assert(false);
						}
					}
				}
			}
		}

#if MAC
		static CancellationTokenSource? monitorCancellation;
		static SemaphoreSlim? networkChanged;

		static void AddNetworkChangeHandler()
		{
			monitorCancellation = new CancellationTokenSource();
			networkChanged = new SemaphoreSlim(0, 1);
			NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
			_ = MonitorAvailability(monitorCancellation, networkChanged);
		}

		static void RemoveNetworkChangeHandler()
		{
			NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
			monitorCancellation?.Cancel();
			monitorCancellation = null;
			networkChanged = null;
			oldIsNetworkAvailable = false;
		}

		static void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
		{
			lock (staticLock)
			{
				if (networkChanged != null && networkChanged.CurrentCount == 0)
					networkChanged.Release();
			}
		}

		static async Task MonitorAvailability(CancellationTokenSource source, SemaphoreSlim changed)
		{
			try
			{
				while (!source.IsCancellationRequested)
				{
					bool available = await CanConnect(source.Token).ConfigureAwait(false);
					EventHandler<EventArgs>? handler = null;
					lock (staticLock)
					{
						if (ReferenceEquals(monitorCancellation, source) && oldIsNetworkAvailable != available)
						{
							oldIsNetworkAvailable = available;
							handler = networkAvailabilityChanged;
						}
					}
					handler?.Invoke(null, EventArgs.Empty);
					await changed.WaitAsync(TimeSpan.FromSeconds(30), source.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (source.IsCancellationRequested)
			{
			}
			finally
			{
				changed.Dispose();
				source.Dispose();
			}
		}

		static async Task<bool> CanConnect(CancellationToken cancellationToken)
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(5));
			using var client = new TcpClient();
			try
			{
				await client.ConnectAsync("www.google.com", 443, timeout.Token).ConfigureAwait(false);
				return true;
			}
			catch (SocketException)
			{
				return false;
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}

#endif
	}

	static class Extensions
	{
		public static bool HasNonLoopbackDns(this IPInterfaceProperties ipProperties)
		{
			var dnsAddresses = ipProperties.DnsAddresses;
			if (dnsAddresses?.Count > 0)
				foreach (var dns in dnsAddresses)
					if (!IPAddress.IsLoopback(dns))
						return true;
			return false;
		}
	}
}
