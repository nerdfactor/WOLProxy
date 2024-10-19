using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// Simple implementation of a WOL packet sender.
/// </summary>
public class SimpleWolPacketSender : IWolPacketSender {

	protected readonly ILogger<IWolPacketSender> _logger;
	protected readonly AppSettings _settings;

	public SimpleWolPacketSender(ILogger<SimpleWolPacketSender> logger, AppSettings settings) {
		this._logger = logger;
		this._settings = settings;
	}

	/// <summary>
	/// Send a default WOL packet to the given MAC address on network adapters.
	/// </summary>
	/// <param name="macAddress"></param>
	/// <param name="adapters"></param>
	public void SendWolPacket(string macAddress, List<NetworkAdapter> adapters) {
		byte[] wolPacket = this.CreateWolPacket(macAddress);
		this.SendWolPacket(wolPacket, macAddress, adapters);
	}

	/// <inheritdoc cref="IWolPacketSender.SendWolPacket(byte[],string,List{NetworkAdapter})" />
	public virtual void SendWolPacket(byte[] packet, string macAddress, List<NetworkAdapter> adapters) {
		adapters.ForEach(adapter => this.SendWolPacketToAdapter(packet, adapter));
		this._logger.LogInformation("Sent WOL packet to MAC address {MacAddress}", macAddress);
	}

	/// <summary>
	/// Send a WOL packet to the given adapter.
	/// </summary>
	/// <param name="packet"></param>
	/// <param name="adapter"></param>
	/// <returns></returns>
	protected Task SendWolPacketToAdapter(byte[] packet, NetworkAdapter adapter) {
		UdpClient client = new UdpClient();
		client.EnableBroadcast = true;
		try {
			this._logger.LogInformation("Sending WOL packet to broadcast address {BroadcastAddress}", adapter.Broadcast);
			for (int i = 0; i < this._settings.RepeatSend; i++) {
				client.SendAsync(packet, packet.Length, new IPEndPoint(adapter.Broadcast, this._settings.OutgoingPort));
			}
		} catch (Exception ex) {
			this._logger.LogError(ex, "Error sending WOL packet to broadcast address {BroadcastAddress}", adapter.Broadcast);
			client.Dispose();
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Create a default WOL packet for the given MAC address. The packet contains
	/// 6 bytes of all 255 (0xFF), followed by sixteen repetitions of the target
	/// 48-bit MAC address, for a total of 102 bytes.
	/// </summary>
	/// <param name="mac"></param>
	/// <returns></returns>
	public byte[] CreateWolPacket(string mac) {
		mac = mac.Trim().Replace(":", "").Replace("-", "");
		byte[] bytes = new byte[102];
		int counter = 0;

		// Fill the first 6 bytes with 0xFF
		for (int y = 0; y < 6; y++) {
			bytes[counter++] = 0xFF;
		}

		// Append the MAC address 16 times
		for (int y = 0; y < 16; y++) {
			for (int z = 0; z < 6; z++) {
				bytes[counter++] = byte.Parse(mac.Substring(z * 2, 2), NumberStyles.HexNumber);
			}
		}

		return bytes;
	}

}

/// <summary>
/// Interface for sending WOL packets.
/// </summary>
public interface IWolPacketSender {

	/// <summary>
	/// Sending a WOL packet to the given MAC address on network adapters. The
	/// packet will be created using the CreateWolPacket method.
	/// </summary>
	/// <param name="macAddress"></param>
	/// <param name="adapters"></param>
	public void SendWolPacket(string macAddress, List<NetworkAdapter> adapters);

	/// <summary>
	/// Sending a specific WOL packet to the given MAC address on network adapters.
	/// </summary>
	/// <param name="packet"></param>
	/// <param name="macAddress"></param>
	/// <param name="adapters"></param>
	public void SendWolPacket(byte[] packet, string macAddress, List<NetworkAdapter> adapters);

	/// <summary>
	/// Creates a WOL packet for the given MAC address.
	/// </summary>
	/// <param name="mac"></param>
	/// <returns></returns>
	public byte[] CreateWolPacket(string mac);

}

/// <summary>
/// Wrapper for a WOL packet request containing the packet, MAC address and
/// used network adapters.
/// </summary>
/// <param name="Packet"></param>
/// <param name="MacAddress"></param>
/// <param name="Adapters"></param>
public record WolPacketRequest(byte[] Packet, string MacAddress, IEnumerable<NetworkAdapter> Adapters);