using Godot;
using System.Collections.Generic;
using System.Linq;

public enum QuestProviderKind
{
	Unknown = 0,
	ServiceContractBoard,
	ServiceBountyRegistry,
	Npc,
}

public enum QuestListingChannel
{
	Unknown = 0,
	ContractBoard,
	BountyRegistry,
	NpcOffer,
}

public static class QuestProviderContentRules
{
	private static readonly StringName ProviderContractBoard = "service_contract_board";
	private static readonly StringName ProviderBountyRegistry = "service_bounty_registry";
	private static readonly StringName ProviderNpc = "npc";

	private static readonly StringName ChannelContractBoard = "contract_board";
	private static readonly StringName ChannelBountyRegistry = "bounty_registry";
	private static readonly StringName ChannelNpcOffer = "npc_offer";

	public static QuestProviderKind ToProviderKind(QuestDef questDef)
	{
		StringName kind = questDef.provider_kind;
		if (kind == ProviderContractBoard) return QuestProviderKind.ServiceContractBoard;
		if (kind == ProviderBountyRegistry) return QuestProviderKind.ServiceBountyRegistry;
		if (kind == ProviderNpc) return QuestProviderKind.Npc;
		return QuestProviderKind.Unknown;
	}

	public static Godot.Collections.Array<QuestListingChannel> ToListingChannels(QuestDef questDef)
	{
		var result = new Godot.Collections.Array<QuestListingChannel>();
		foreach (StringName channel in questDef.listing_channels)
		{
			result.Add(channel switch
			{
				_ when channel == ChannelContractBoard => QuestListingChannel.ContractBoard,
				_ when channel == ChannelBountyRegistry => QuestListingChannel.BountyRegistry,
				_ when channel == ChannelNpcOffer => QuestListingChannel.NpcOffer,
				_ => QuestListingChannel.Unknown,
			});
		}
		return result;
	}

	public static bool IsSupportedProviderKind(QuestProviderKind kind) =>
		kind is QuestProviderKind.ServiceContractBoard
			or QuestProviderKind.ServiceBountyRegistry
			or QuestProviderKind.Npc;

	public static bool IsSupportedListingChannel(QuestListingChannel channel) =>
		channel is QuestListingChannel.ContractBoard
			or QuestListingChannel.BountyRegistry
			or QuestListingChannel.NpcOffer;

	public static IReadOnlyList<StringName> SupportedProviderIds() =>
		new List<StringName>
		{
			ProviderContractBoard,
			ProviderBountyRegistry,
		};

	public static bool IsSupportedProviderId(StringName value) =>
		SupportedProviderIds().Contains(value);

	public static string SupportedProviderLabel()
	{
		return "service_bounty_registry, service_contract_board";
	}
}
