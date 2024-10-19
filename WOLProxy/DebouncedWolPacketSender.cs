using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace eu.nerdfactor.WOLProxy;

/// <summary>
/// Implementation of a WOL packet sender that debounces sending packets and
/// restricts repeated sending to the same MAC address.
/// </summary>
public class DebouncedWolPacketSender : SimpleWolPacketSender, IDisposable {

	private readonly ConcurrentDictionary<string, DateTime> _macAddressLastSentTime;
	private readonly ConcurrentQueue<WolPacketRequest> _wolPacketQueue;
	private readonly TimeSpan _debounceTime;
	private readonly TimeSpan _macAddressExpirationTime;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _processingTask;
	private readonly Task _cleanupTask;

	public DebouncedWolPacketSender(ILogger<DebouncedWolPacketSender> logger, AppSettings settings) : base(logger, settings) {
		this._macAddressLastSentTime = new ConcurrentDictionary<string, DateTime>();
		this._wolPacketQueue = new ConcurrentQueue<WolPacketRequest>();
		this._debounceTime = TimeSpan.FromMilliseconds(this._settings.DebounceTime);
		this._macAddressExpirationTime = TimeSpan.FromMilliseconds(this._settings.MacAddressExpirationTime);
		this._cancellationTokenSource = new CancellationTokenSource();

		this._processingTask = Task.Run(() => this.ProcessWolPacketQueue(this._cancellationTokenSource.Token));
		this._cleanupTask = Task.Run(() => this.CleanupOldMacAddresses(this._cancellationTokenSource.Token));
	}

	/// <summary>
	/// Send a WOL packet to the given MAC address on network adapters. Will
	/// enqueue the packet requests to debounce sending. Will also track the
	/// last time a packet was sent to the MAC address to prevent repeated
	/// sending.
	/// </summary>
	/// <param name="packet"></param>
	/// <param name="macAddress"></param>
	/// <param name="adapters"></param>
	public override void SendWolPacket(byte[] packet, string macAddress, List<NetworkAdapter> adapters) {
		if (this.CanSendToMacAddress(macAddress)) {
			this._wolPacketQueue.Enqueue(new WolPacketRequest(packet, macAddress, adapters));
			this.UpdateLastSentTime(macAddress);
			this._logger.LogInformation("Enqueued WOL packet for MAC address {MacAddress}", macAddress);
		} else {
			this._logger.LogInformation("Skipping WOL packet for MAC address {MacAddress} due to debounce time", macAddress);
		}
	}

	/// <summary>
	/// Check if it's okay to send a WOL packet to the given MAC address.
	/// </summary>
	/// <param name="macAddress"></param>
	/// <returns></returns>
	private bool CanSendToMacAddress(string macAddress) {
		if (!this._macAddressLastSentTime.TryGetValue(macAddress, out DateTime lastSentTime)) {
			return true;
		}

		TimeSpan timeSinceLastSend = DateTime.Now - lastSentTime;
		return timeSinceLastSend >= this._debounceTime;
	}

	/// <summary>
	/// Update the last sent time for the given MAC address.
	/// </summary>
	/// <param name="macAddress"></param>
	private void UpdateLastSentTime(string macAddress) {
		this._macAddressLastSentTime[macAddress] = DateTime.Now;
	}

	/// <summary>
	/// Process the WOL packet queue and send packets to all network adapters.
	/// </summary>
	/// <param name="cancellationToken"></param>
	private async Task ProcessWolPacketQueue(CancellationToken cancellationToken) {
		while (!cancellationToken.IsCancellationRequested) {
			if (this._wolPacketQueue.TryDequeue(out WolPacketRequest? packetRequest)) {
				await this.SendWolPacketToAllAdapters(packetRequest.Packet, packetRequest.Adapters);
				this._logger.LogInformation("Sent WOL packet to MAC address {MacAddress}", packetRequest.MacAddress);
			} else {
				await Task.Delay(100, cancellationToken);
			}
		}
	}

	/// <summary>
	/// Send a WOL packet to all network adapters. Will exclude adapters that
	/// are disabled due to the settings.
	/// </summary>
	/// <param name="packet"></param>
	/// <param name="adapters"></param>
	private async Task SendWolPacketToAllAdapters(byte[] packet, IEnumerable<NetworkAdapter> adapters) {
		foreach (NetworkAdapter adapter in adapters) {
			await this.SendWolPacketToAdapter(packet, adapter);
		}
	}

	/// <summary>
	/// Periodically clean up old MAC addresses from the last sent tracking.
	/// </summary>
	/// <param name="cancellationToken"></param>
	private async Task CleanupOldMacAddresses(CancellationToken cancellationToken) {
		while (!cancellationToken.IsCancellationRequested) {
			await Task.Delay(this._settings.MacAddressCleanupInterval, cancellationToken);

			DateTime now = DateTime.Now;

			foreach (KeyValuePair<string, DateTime> entry in this._macAddressLastSentTime) {
				if (now - entry.Value >= this._macAddressExpirationTime) {
					this._macAddressLastSentTime.TryRemove(entry.Key, out _);
					this._logger.LogInformation("Cleaned up MAC address {MacAddress} from last sent tracking.", entry.Key);
				}
			}
		}
	}

	/// <summary>
	/// Dispose of the debounced WOL packet sender.
	/// </summary>
	public void Dispose() {
		this._cancellationTokenSource.Cancel();
		try {
			Task.WaitAll(this._processingTask, this._cleanupTask);
		} catch (OperationCanceledException) {
			// Expected exception when tasks are canceled
		} catch (AggregateException ex) {
			if (ex.InnerException is OperationCanceledException) {
				// Expected exception when tasks are canceled
			} else {
				this._logger.LogError(ex, "Error occurred while disposing DebouncedWOLPacketSender.");
			}
		} finally {
			this._cancellationTokenSource.Dispose();
		}
	}

	/// <summary>
	/// Destructor for the debounced WOL packet sender using the Dispose pattern.
	/// </summary>
	~DebouncedWolPacketSender() {
		this.Dispose();
	}

}