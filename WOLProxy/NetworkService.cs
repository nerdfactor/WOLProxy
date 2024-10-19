using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// Service for basic network operations.
/// </summary>
public class NetworkService : INetworkService {

	private readonly ILogger<NetworkService> _logger;
	private readonly AppSettings _settings;

	public List<NetworkAdapter> Adapters { get; } = [];
	public ReadOnlyCollection<NetworkAdapter> IncomingAdapters { get; private set; } = new ReadOnlyCollection<NetworkAdapter>([]);
	public ReadOnlyCollection<NetworkAdapter> OutgoingAdapters { get; private set; } = new ReadOnlyCollection<NetworkAdapter>([]);

	public NetworkService(ILogger<NetworkService> logger, AppSettings settings) {
		this._logger = logger;
		this._settings = settings;
		this.FindNetworkAdapters();
		this.FilterIncomingAdapters();
		this.FilterOutgoingAdapters();
	}

	/// <summary>
	/// Find all local network adapters and store them in the Adapters list.
	/// </summary>
	private void FindNetworkAdapters() {
		this._logger.LogInformation("Finding network adapters...");

		IEnumerable<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces()
			.Where(
				ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
				      ni.OperationalStatus == OperationalStatus.Up
			);

		foreach (NetworkInterface networkInterface in interfaces) {
			IEnumerable<UnicastIPAddressInformation> unicastIpInfoCol = networkInterface.GetIPProperties().UnicastAddresses
				.Where(ip => ip.IPv4Mask.ToString() != "0.0.0.0");

			foreach (UnicastIPAddressInformation unicastIpInfo in unicastIpInfoCol) {
				IPAddress? broadcastAddress = this.CalculateBroadcastAddress(unicastIpInfo.Address, unicastIpInfo.IPv4Mask);
				if (broadcastAddress == null) {
					continue;
				}

				this.Adapters.Add(
					new NetworkAdapter(
						unicastIpInfo.Address,
						unicastIpInfo.IPv4Mask,
						broadcastAddress,
						unicastIpInfo.Address.ToString() == this._settings.PrimaryAdapter
					)
				);
				this._logger.LogInformation("Found network adapter: {Adapter} ({IpAddress})", networkInterface.Name, unicastIpInfo.Address);
			}
		}

		// Set the first adapter as the primary if none is set
		if (this.GetPrimaryAdapter() == null && this.Adapters.Count > 0) {
			this.Adapters[0].IsPrimary = true;
		}

		this._logger.LogInformation("Found {AdapterCount} network adapters.", this.Adapters.Count);
	}

	/// <summary>
	/// Filter incoming adapters based on the settings.
	/// </summary>
	private void FilterIncomingAdapters() {
		this.IncomingAdapters = new ReadOnlyCollection<NetworkAdapter>(
			this.Adapters.Where(
				adapter => {
					if (this._settings.PrimaryOnly && !adapter.IsPrimary) {
						return false;
					}

					if (this._settings.UseIncomingAdapters.Count > 0 && !this._settings.UseIncomingAdapters.Contains(adapter.Address.ToString())) {
						return false;
					}

					return true;
				}
			).ToList()
		);
	}

	/// <summary>
	/// Filter outgoing adapters based on the settings.
	/// </summary>
	private void FilterOutgoingAdapters() {
		this.OutgoingAdapters = new ReadOnlyCollection<NetworkAdapter>(
			this.Adapters.Where(
				adapter => {
					if (this._settings.PrimaryOnly && !adapter.IsPrimary) {
						return false;
					}

					if (this._settings.UseOutgoingAdapters.Count > 0 && !this._settings.UseOutgoingAdapters.Contains(adapter.Address.ToString())) {
						return false;
					}

					return true;
				}
			).ToList()
		);
	}

	/// <summary>
	/// Calculate the broadcast address for the given IP address and subnet mask.
	/// </summary>
	/// <param name="address"></param>
	/// <param name="subnetMask"></param>
	/// <returns>The calculated broadcast address or null if the calculation failed.</returns>
	private IPAddress? CalculateBroadcastAddress(IPAddress address, IPAddress subnetMask) {
		byte[] ipAddressBytes = address.GetAddressBytes();
		byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

		if (ipAddressBytes.Length != subnetMaskBytes.Length) {
			return null;
		}

		byte[] broadcastAddress = new byte[ipAddressBytes.Length];
		for (int i = 0; i < broadcastAddress.Length; i++) {
			broadcastAddress[i] = (byte)(ipAddressBytes[i] | (subnetMaskBytes[i] ^ 255));
		}

		return new IPAddress(broadcastAddress);
	}


	/// <summary>
	/// Get the primary network adapter.
	/// </summary>
	/// <returns></returns>
	public NetworkAdapter? GetPrimaryAdapter() {
		return this.Adapters.FirstOrDefault(a => a is { IsPrimary: true }, null);
	}

}

/// <summary>
/// Service interface for basic network operations.
/// </summary>
public interface INetworkService {

	public List<NetworkAdapter> Adapters { get; }
	public ReadOnlyCollection<NetworkAdapter> IncomingAdapters { get; }
	public ReadOnlyCollection<NetworkAdapter> OutgoingAdapters { get; }
	public NetworkAdapter? GetPrimaryAdapter();

}

/// <summary>
/// Network adapter information.
/// </summary>
/// <param name="Address"></param>
/// <param name="Mask"></param>
/// <param name="Broadcast"></param>
/// <param name="IsPrimary"></param>
public record NetworkAdapter(IPAddress Address, IPAddress Mask, IPAddress Broadcast, bool IsPrimary = false) {

	public bool IsPrimary { get; set; } = false;

}