using System.Collections.ObjectModel;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// The settings for the application.
/// </summary>
public class AppSettings {

	/// <summary>
	/// The port used for outgoing WOL packets.
	/// </summary>
	public int OutgoingPort { get; init; } = 9;

	/// <summary>
	/// If the UDP listener is enabled.
	/// </summary>
	public bool UpdListenerEnabled { get; init; } = true;

	/// <summary>
	/// The UDP port used for incoming WOL packets. Only used if UdpListenerEnabled is true.
	/// </summary>
	public int UdpPort { get; init; } = 9;

	/// <summary>
	/// If the TCP listener is enabled.
	/// </summary>
	public bool TcpListenerEnabled { get; init; } = false;

	/// <summary>
	/// The TCP port used for incoming WOL packets. Only used if TcpListenerEnabled is true.
	/// </summary>
	public int TcpPort { get; init; } = 9;

	/// <summary>
	/// Pattern to match a WOL packet. Will only be used for incoming packets.
	/// Outgoing packets will always be sent as standard magic packet.
	/// </summary>
	public string WolDataPattern { get; init; } = "^(FF){6}([0-9A-F]{2}){12}$";

	/// <summary>
	/// Pattern to match a MAC address.
	/// </summary>
	public string MacAddressPattern { get; init; } = "^([0-9A-F]{2}[:-]){5}([0-9A-F]{2})$";

	/// <summary>
	/// Time in milliseconds to wait between sending WOL packets.
	/// </summary>
	public int DebounceTime { get; init; } = 1000;

	/// <summary>
	/// Time in milliseconds after which a new WOL Packet may be sent to a MAC address.
	/// </summary>
	public int MacAddressExpirationTime { get; init; } = 60000;

	/// <summary>
	/// Time in milliseconds to wait between cleaning up old MAC addresses.
	/// </summary>
	public int MacAddressCleanupInterval { get; init; } = 60000;

	/// <summary>
	/// The primary network adapter used.
	/// </summary>
	public string PrimaryAdapter { get; init; } = "";

	/// <summary>
	/// If only the primary adapter should be used to listen for and send WOL packets.
	/// </summary>
	public bool PrimaryOnly { get; init; } = false;

	/// <summary>
	/// If new WOL packets should be sent on the adapter they were received on.
	/// </summary>
	public bool SendBackToAdapter { get; init; } = true;

	/// <summary>
	/// A list of ip addresses of the adapters to listen for incoming WOL packets.
	/// </summary>
	public ReadOnlyCollection<string> UseIncomingAdapters { get; init; } = new ReadOnlyCollection<string>([]);

	/// <summary>
	/// A list of ip addresses of the adapters to send outgoing WOL packets.
	/// </summary>
	public ReadOnlyCollection<string> UseOutgoingAdapters { get; init; } = new ReadOnlyCollection<string>([]);

	/// <summary>
	/// A list of trusted sources to accept WOL packets from.
	/// </summary>
	public ReadOnlyCollection<string> TrustedSources { get; init; } = new ReadOnlyCollection<string>([]);

	/// <summary>
	/// Amount of times to repeat sending a WOL packet.
	/// </summary>
	public int RepeatSend { get; init; } = 1;

}