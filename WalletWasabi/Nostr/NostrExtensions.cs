﻿using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Secp256k1;
using NNostr.Client;

namespace WalletWasabi.Nostr;

public record NostrCoordinator(string Description, string Name, Uri Uri, Network Network);

public record GingerNostrCoordinator()
	: NostrCoordinator(
		Description: "Ginger Coordinator | FREE Remix, FREE under 0.01 BTC, FREE for Wasabi mixed coins | SAFE COINJOINS - Illicit actors are not allowed to participate!",
		Name: "Ginger Coordinator",
		Uri: new Uri("https://api.gingerwallet.io/"),
		Network: Network.Main);

public static class NostrExtensions
{
	private const int Kind = 15750;
	private const string TypeTagIdentifier = "type";
	private const string TypeTagValue = "wabisabi";
	private const string NetworkTagIdentifier = "network";
	private const string EndpointTagIdentifier = "endpoint";

	public static INostrClient Create(Uri[] relays, EndPoint? torEndpoint = null)
	{
		var webProxy = torEndpoint is IPEndPoint endpoint
			? new WebProxy($"socks5://{endpoint.Address}:{endpoint.Port}")
			: torEndpoint is DnsEndPoint endpoint2
				? new WebProxy($"socks5://{endpoint2.Host}:{endpoint2.Port}")
				: null;
		return Create(relays, webProxy);

	}

	public static INostrClient Create(Uri[] relays, WebProxy? proxy = null)
	{
		void ConfigureSocket(WebSocket socket)
		{
			if (socket is ClientWebSocket clientWebSocket && proxy != null)
			{
				clientWebSocket.Options.Proxy = proxy;
			}
		}

		return relays.Length switch
		{
			0 => throw new ArgumentException("At least one relay is required.", nameof(relays)),
			1 => new NostrClient(relays.First(), ConfigureSocket),
			_ => new CompositeNostrClient(relays, ConfigureSocket)
		};
	}

	public static async Task PublishAsync(
		this INostrClient client,
		NostrEvent[] evts,
		CancellationToken cancellationToken)
	{
		if (!evts.Any())
		{
			return;
		}

		await client.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);
		await client.SendEventsAndWaitUntilReceived(evts, cancellationToken).ConfigureAwait(false);
	}

	public static async Task<NostrEvent> CreateCoordinatorDiscoveryEventAsync(
		this ECPrivKey key,
		NostrCoordinator coordinator)
	{
		var evt = new NostrEvent()
		{
			Kind = Kind,
			Content = coordinator.Description,
			Tags =
			[
				new() {TagIdentifier = EndpointTagIdentifier, Data = [coordinator.Uri.ToString()]},
				new() {TagIdentifier = TypeTagIdentifier, Data = [TypeTagValue]},
				new()
				{
					TagIdentifier = NetworkTagIdentifier,
					Data = [coordinator.Network.ChainName.ToString().ToLower()]
				}
			]
		};

		await evt.ComputeIdAndSignAsync(key).ConfigureAwait(false);
		return evt;
	}
}
