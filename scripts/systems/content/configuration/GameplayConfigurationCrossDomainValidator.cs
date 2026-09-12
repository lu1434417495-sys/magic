#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class GameplayConfigurationCrossDomainValidator
{
    internal static IReadOnlyList<string> Validate(
        GameplayConfigurationDefinition configuration,
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> brains,
        IReadOnlyDictionary<StringName, RaceDefinition> races,
        IReadOnlyDictionary<StringName, SubraceDefinition> subraces,
        IReadOnlyDictionary<StringName, AgeProfileDefinition> ageProfiles
    )
    {
        var errors = new List<string>();
        foreach (AchievementDefinition achievement in configuration.Achievements.Values)
        foreach (AchievementRewardDefinition reward in achievement.Rewards)
        {
            if (
                PendingCharacterRewardContentRules.RequiresSkillTarget(reward.RewardType)
                && !skills.ContainsKey(reward.TargetId)
            )
                errors.Add($"Achievement {achievement.AchievementId} references missing skill {reward.TargetId}.");
            if (
                reward.RewardKind
                    is PendingCharacterRewardEntryKind.AttributeDelta
                        or PendingCharacterRewardEntryKind.AttributeProgress
                && !PendingCharacterRewardContentRules.IsValidAttributeTarget(
                    reward.RewardKind,
                    reward.TargetId
                )
            )
                errors.Add(
                    $"Achievement {achievement.AchievementId} {reward.RewardType} references invalid attribute {reward.TargetId}."
                );
        }
        foreach (SettlementShopDefinition shop in configuration.SettlementShops)
        {
            foreach (SettlementShopItemDefinition item in shop.GuaranteedItems)
                RequireItem(errors, items, shop.ShopId, item.ItemId);
            foreach (SettlementShopItemDefinition item in shop.RandomPool)
                RequireItem(errors, items, shop.ShopId, item.ItemId);
        }
        NewGamePartyDefinition party = configuration.NewGameParty;
        foreach (NewGameMemberDefinition member in party.Members)
        {
            if (!races.ContainsKey(member.RaceId))
                errors.Add($"New-game member {member.MemberId} references missing race {member.RaceId}.");
            if (!subraces.ContainsKey(member.SubraceId))
                errors.Add($"New-game member {member.MemberId} references missing subrace {member.SubraceId}.");
            if (!ageProfiles.ContainsKey(member.AgeProfileId))
                errors.Add($"New-game member {member.MemberId} references missing age profile {member.AgeProfileId}.");
            foreach (StringName skillId in member.StartingSkillIds)
                if (!skills.ContainsKey(skillId))
                    errors.Add($"New-game member {member.MemberId} references missing starting skill {skillId}.");
        }
        foreach (NewGameStartingWeaponRuleDefinition rule in party.StartingWeaponRules)
            RequireWeapon(errors, items, rule.ItemId, "starting weapon rule");
        RequireWeapon(errors, items, party.StartingWeaponFallbackItemId, "starting weapon fallback");

        RequireSkill(
            errors,
            skills,
            configuration.BattleSkillRoles.BasicAttackSkillId,
            "basic attack role"
        );
        SkillGenerationBattleSimFixtureDefinition skillFixture = configuration.SkillGenerationBattleSim;
        RequireSkill(errors, skills, skillFixture.GroundBenchmarkSkillId, "skill generation ground benchmark");
        RequireSkill(errors, skills, skillFixture.MultiTargetBenchmarkSkillId, "skill generation multi-target benchmark");
        RequireSkill(errors, skills, skillFixture.RangedUnitBenchmarkSkillId, "skill generation ranged-unit benchmark");
        RequireBrain(errors, brains, skillFixture.MeleeBrainId, "skill generation melee brain");
        RequireBrain(errors, brains, skillFixture.MageBrainId, "skill generation mage brain");
        RequireBrain(errors, brains, skillFixture.RangedBrainId, "skill generation ranged brain");
        EquipmentGenerationBattleSimFixtureDefinition equipmentFixture = configuration.EquipmentGenerationBattleSim;
        RequireBrain(errors, brains, equipmentFixture.MeleeBrainId, "equipment generation melee brain");
        return errors;
    }

    private static void RequireItem(List<string> errors, IReadOnlyDictionary<StringName, ItemDefinition> items, StringName shopId, StringName itemId)
    {
        if (!items.TryGetValue(itemId, out ItemDefinition? item))
        {
            errors.Add($"Settlement shop {shopId} references missing item {itemId}.");
            return;
        }
        // 买价为 0 的商品在 BuildShopEntry 里会被无声剔除，商店直接少一格备货。
        if (item?.BuyPrice is not > 0)
            errors.Add(
                $"Settlement shop {shopId} stocks item {itemId} with a non-positive buy_price; "
                    + "it could never be offered."
            );
    }

    private static void RequireWeapon(List<string> errors, IReadOnlyDictionary<StringName, ItemDefinition> items, StringName itemId, string label)
    {
        if (!items.TryGetValue(itemId, out ItemDefinition? item) || item?.IsWeapon() != true)
            errors.Add($"Gameplay configuration {label} references missing/non-weapon item {itemId}.");
    }

    private static void RequireSkill(List<string> errors, IReadOnlyDictionary<StringName, SkillDefinition> skills, StringName id, string label)
    {
        if (!skills.ContainsKey(id)) errors.Add($"Gameplay configuration {label} references missing skill {id}.");
    }

    private static void RequireBrain(List<string> errors, IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> brains, StringName id, string label)
    {
        if (!brains.ContainsKey(id)) errors.Add($"Gameplay configuration {label} references missing brain {id}.");
    }
}
