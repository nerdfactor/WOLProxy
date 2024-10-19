using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// Service that handles incoming WOL packets and forwards them to the selected
/// outgoing adapters.
/// </summary>
public class ProxyService : BackgroundService {

	private readonly ILogger<ProxyService> _logger;
	private readonly INetworkService _networkService;
	private readonly IWolPacketSender _packetSender;
	private readonly AppSettings _settings;

	public ProxyService(ILogger<ProxyService> logger, INetworkService networkService, IWolPacketSender packetSender, AppSettings settings) {
		this._logger = logger;
		this._networkService = networkService;
		this._packetSender = packetSender;
		this._settings = settings;
	}

	/// <summary>
	/// Start the proxy service. Will set up listeners for incoming UDP and TCP
	/// packets on the configured ports.
	/// </summary>
	/// <param name="stoppingToken"></param>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		List<Task> udpTasks = [];
		List<Task> tcpTasks = [];

		foreach (NetworkAdapter adapter in this._networkService.IncomingAdapters) {
			if (this._settings.UpdListenerEnabled) {
				udpTasks.Add(this.StartUdpListener(adapter, stoppingToken));
			}

			if (this._settings.TcpListenerEnabled) {
				tcpTasks.Add(this.StartTcpListener(adapter, stoppingToken));
			}
		}

		await Task.WhenAll(udpTasks);
		await Task.WhenAll(tcpTasks);
	}

	/// <summary>
	/// Stop the proxy service.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public override Task StopAsync(CancellationToken cancellationToken) {
		this._logger.LogInformation("Proxy Service is stopping at: {Time}", DateTimeOffset.Now);
		return base.StopAsync(cancellationToken);
	}

	/// <summary>
	/// Start listening for incoming WOL packets on the specified UDP port for
	/// the given adapter.
	/// </summary>
	/// <param name="adapter"></param>
	/// <param name="cancellationToken"></param>
	private async Task StartUdpListener(NetworkAdapter adapter, CancellationToken cancellationToken) {
		using UdpClient udpClient = new UdpClient(adapter.Address.ToString(), this._settings.UdpPort);
		this._logger.LogInformation("Listening for WOL packets on UDP port {Port} for adapter {Adapter}", this._settings.UdpPort, adapter.Address);

		while (!cancellationToken.IsCancellationRequested) {
			UdpReceiveResult result = await udpClient.ReceiveAsync(cancellationToken);
			this.ProcessIncomingUdpRequest(result, adapter);
		}
	}

	/// <summary>
	/// Process the incoming UDP request by extracting the MAC address from an
	/// incoming WOL packet and repeating a WOL packet to the selected outgoing
	/// adapters.
	/// </summary>
	/// <param name="result"></param>
	/// <param name="adapter"></param>
	private void ProcessIncomingUdpRequest(UdpReceiveResult result, NetworkAdapter adapter) {
		byte[] receivedPacket = result.Buffer;

		string sourceAddress = result.RemoteEndPoint.Address.ToString();
		if (this._settings.TrustedSources.Count > 0 && !this._settings.TrustedSources.Contains(sourceAddress)) {
			this._logger.LogWarning("Received packet from untrusted source: {SourceAddress}", sourceAddress);
			return;
		}

		if (this.IsWolPacket(receivedPacket)) {
			string? macAddress = this.ExtractMacAddress(receivedPacket);
			if (string.IsNullOrEmpty(macAddress)) {
				return;
			}

			this._logger.LogInformation("Received WOL packet for MAC: {MAC} on UDP", macAddress);
			this._packetSender.SendWolPacket(macAddress, this.SelectOutgoingAdapters(adapter));
		} else {
			this._logger.LogWarning("Received packet is not a valid WOL packet on UDP.");
		}
	}


	/// <summary>
	/// Start listening for incoming WOL packets on the specified TCP port for
	/// the given adapter.
	/// </summary>
	/// <param name="adapter"></param>
	/// <param name="cancellationToken"></param>
	private async Task StartTcpListener(NetworkAdapter adapter, CancellationToken cancellationToken) {
		using TcpListener tcpListener = new TcpListener(adapter.Address, this._settings.TcpPort);
		tcpListener.Start();
		this._logger.LogInformation("Listening for WOL packets on TCP port {Port} for adapter {Adapter}.", this._settings.TcpPort, adapter.Address);

		while (!cancellationToken.IsCancellationRequested) {
			try {
				TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync(cancellationToken);
				_ = this.ProcessIncomingTcpRequestAsync(tcpClient, adapter, cancellationToken);
			} catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted) {
				break;
			} catch (OperationCanceledException) {
				break;
			} catch (Exception ex) {
				this._logger.LogError("Error accepting TCP connection on adapter {Adapter}: {Message}.", adapter.Address, ex.Message);
			}
		}
	}

	/// <summary>
	/// Process the incoming TCP request by extracting the MAC address from an
	/// incoming WOL packet and repeating a WOL packet to the selected outgoing
	/// adapters.
	/// </summary>
	/// <param name="client"></param>
	/// <param name="adapter"></param>
	/// <param name="cancellationToken"></param>
	private async Task ProcessIncomingTcpRequestAsync(TcpClient client, NetworkAdapter adapter, CancellationToken cancellationToken) {
		using (client) {
			byte[] buffer = new byte[1024];
			NetworkStream stream = client.GetStream();
			int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
			if (bytesRead > 0) {
				byte[] receivedPacket = new byte[bytesRead];
				Array.Copy(buffer, receivedPacket, bytesRead);

				IPEndPoint? sourceEndPoint = (IPEndPoint?)client.Client.RemoteEndPoint;
				if (sourceEndPoint != null && this._settings.TrustedSources.Count > 0 && !this._settings.TrustedSources.Contains(sourceEndPoint.Address.ToString())) {
					this._logger.LogWarning("Received packet from untrusted source: {SourceAddress}", sourceEndPoint.Address.ToString());
					return;
				}

				if (this.IsWolPacket(receivedPacket)) {
					string? macAddress = this.ExtractMacAddress(receivedPacket);
					if (!string.IsNullOrEmpty(macAddress)) {
						this._logger.LogInformation("Received WOL packet from TCP for MAC: {MAC} on adapter {Adapter}", macAddress, adapter.Address);
						this._packetSender.SendWolPacket(macAddress, this.SelectOutgoingAdapters(adapter));
					}
				} else {
					this._logger.LogWarning("Received TCP packet is not a valid WOL packet on adapter {Adapter}.", adapter.Address);
				}
			}
		}
	}

	/// <summary>
	/// Select the outgoing adapters based on the incoming adapter and the settings.
	/// </summary>
	/// <param name="incomingAdapter"></param>
	/// <returns></returns>
	private List<NetworkAdapter> SelectOutgoingAdapters(NetworkAdapter incomingAdapter) {
		return this._networkService.OutgoingAdapters.Where(
			adapter => {
				if (!this._settings.SendBackToAdapter && adapter.Address.ToString() == incomingAdapter.Address.ToString()) {
					return false;
				}

				return false;
			}
		).ToList();
	}

	/// <summary>
	/// Check if the received packet is a valid WOL packet based on configured pattern.
	/// </summary>
	/// <param name="packet"></param>
	/// <returns></returns>
	private bool IsWolPacket(byte[] packet) {
		// Convert the packet to a hexadecimal string
		string packetHex = BitConverter.ToString(packet).Replace("-", string.Empty).Replace(":", string.Empty);

		// Validate the packet against the user-defined WOL regex pattern
		string pattern = this._settings.WolDataPattern;
		return Regex.IsMatch(packetHex, pattern, RegexOptions.IgnoreCase);
	}

	/// <summary>
	/// Extract the MAC address from the received packet based on the configured pattern.
	/// </summary>
	/// <param name="packet"></param>
	/// <returns></returns>
	private string? ExtractMacAddress(byte[] packet) {
		// Convert packet to a hexadecimal string
		string packetHex = BitConverter.ToString(packet).Replace("-", string.Empty);

		// Use a regex to extract the MAC address based on the settings
		string macPattern = this._settings.MacAddressPattern;
		Match match = Regex.Match(packetHex, macPattern, RegexOptions.IgnoreCase);

		return match.Success ? string.Join(":", Enumerable.Range(0, 6).Select(i => match.Groups[i].Value)) : null;
	}

}