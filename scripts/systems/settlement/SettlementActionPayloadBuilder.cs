using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class SettlementActionPayloadBuilder
{
    internal static SettlementActionRequest BuildRequestFromBoundary(
        string fallbackSettlementId,
        string actionId,
        GDictionary payload,
        SettlementSubmissionSource defaultSource
    )
    {
        GDictionary source = payload ?? new GDictionary();
        string normalizedActionId = (actionId ?? "").StripEdges();
        string settlementId = ReadString(
            source,
            "settlement_id",
            fallbackSettlementId ?? ""
        ).StripEdges();
        string serviceId = ReadString(
            source,
            "service_id",
            normalizedActionId
        ).StripEdges();
        int quantity = Math.Max(
            ReadInt(source, "request_quantity", ReadInt(source, "quantity", 0)),
            0
        );
        SettlementSubmissionSource submissionSource = ReadSubmissionSource(source);
        if (
            submissionSource == SettlementSubmissionSource.None
            && defaultSource != SettlementSubmissionSource.None
        )
        {
            submissionSource = defaultSource;
        }

        return new SettlementActionRequest(
            new StringName(settlementId),
            new StringName(string.IsNullOrEmpty(serviceId) ? normalizedActionId : serviceId),
            new StringName(normalizedActionId),
            ReadStringName(source, "member_id"),
            quantity,
            submissionSource
        );
    }

    internal static GDictionary BuildActionPayload(
        GDictionary serviceData,
        SettlementActionRequest request,
        StringName recipeId,
        StringName defaultMemberId
    )
    {
        if (serviceData == null || serviceData.Count == 0)
            return new GDictionary();

        GDictionary payload = BuildBasePayload(request.ActionId.ToString(), serviceData);
        payload["settlement_id"] = request.SettlementId.ToString();
        payload["service_id"] = request.ServiceId.ToString();
        if (!SettlementActionRequest.IsEmpty(request.MemberId))
        {
            string memberId = SettlementActionRequest.ToText(request.MemberId);
            payload["member_id"] = memberId;
            payload["default_member_id"] = memberId;
        }
        if (request.Quantity > 0)
        {
            payload["request_quantity"] = request.Quantity;
            payload["quantity"] = request.Quantity;
        }
        if (!SettlementActionRequest.IsEmpty(recipeId))
            payload["recipe_id"] = SettlementActionRequest.ToText(recipeId);
        string submissionSource = SettlementSubmissionSources.ToPayloadValue(request.Source);
        if (!string.IsNullOrEmpty(submissionSource))
            payload["submission_source"] = submissionSource;
        EnsureMemberId(payload, defaultMemberId);
        return payload;
    }

    internal static GDictionary BuildModalActionPayload(
        string actionId,
        GDictionary serviceData,
        GDictionary overrides,
        string fallbackSettlementId,
        StringName defaultMemberId
    )
    {
        if (serviceData == null || serviceData.Count == 0)
            return new GDictionary();

        GDictionary source = overrides ?? new GDictionary();
        SettlementSubmissionSource submissionSource = ReadSubmissionSource(source);
        if (!IsSupportedModalSubmission(submissionSource))
            return new GDictionary();

        SettlementActionRequest request = BuildRequestFromBoundary(
            fallbackSettlementId,
            actionId,
            source,
            submissionSource
        );
        GDictionary payload = BuildActionPayload(
            serviceData,
            request,
            default,
            defaultMemberId
        );
        CopyIfPresent(payload, source, "submission_source");
        CopyIfPresent(payload, source, "panel_kind");
        CopyIfPresent(payload, source, "state_summary_text");
        switch (submissionSource)
        {
            case SettlementSubmissionSource.ContractBoard:
                CopyIfPresent(payload, source, "quest_id");
                CopyIfPresent(payload, source, "provider_interaction_id");
                CopyIfPresent(payload, source, "confirm_accept");
                break;
            case SettlementSubmissionSource.BountyBoard:
                CopyIfPresent(payload, source, "quest_id");
                CopyIfPresent(payload, source, "provider_interaction_id");
                break;
            case SettlementSubmissionSource.Forge:
                CopyIfPresent(payload, source, "recipe_id");
                break;
            case SettlementSubmissionSource.NpcQuestOffer:
                CopyIfPresent(payload, source, "quest_id");
                CopyIfPresent(payload, source, "confirm_accept");
                break;
        }
        return payload;
    }

    internal static GDictionary BuildValidationPayload(SettlementActionRequest request)
    {
        var payload = new GDictionary
        {
            ["settlement_id"] = request.SettlementId.ToString(),
            ["service_id"] = request.ServiceId.ToString(),
            ["action_id"] = request.ActionId.ToString(),
        };
        if (!SettlementActionRequest.IsEmpty(request.MemberId))
            payload["member_id"] = SettlementActionRequest.ToText(request.MemberId);
        if (request.Quantity > 0)
            payload["request_quantity"] = request.Quantity;
        string submissionSource = SettlementSubmissionSources.ToPayloadValue(request.Source);
        if (!string.IsNullOrEmpty(submissionSource))
            payload["submission_source"] = submissionSource;
        return payload;
    }

    internal static SettlementSubmissionSource ReadSubmissionSource(GDictionary payload)
    {
        return SettlementSubmissionSources.TryParse(
            ReadString(payload, "submission_source"),
            out SettlementSubmissionSource source
        )
            ? source
            : SettlementSubmissionSource.None;
    }

    private static GDictionary BuildBasePayload(string actionId, GDictionary serviceData)
    {
        var payload = new GDictionary
        {
            ["action_id"] = actionId ?? "",
            ["facility_id"] = ReadString(serviceData, "facility_id"),
            ["facility_template_id"] = ReadString(serviceData, "facility_template_id"),
            ["facility_name"] = ReadString(serviceData, "facility_name"),
            ["npc_id"] = ReadString(serviceData, "npc_id"),
            ["npc_template_id"] = ReadString(serviceData, "npc_template_id"),
            ["npc_name"] = ReadString(serviceData, "npc_name"),
            ["service_type"] = ReadString(serviceData, "service_type"),
            ["interaction_script_id"] = ReadString(serviceData, "interaction_script_id"),
        };
        CopyIfPresent(payload, serviceData, "state_label");
        CopyIfPresent(payload, serviceData, "cost_label");
        CopyIfPresent(payload, serviceData, "summary_text");
        CopyIfPresent(payload, serviceData, "disabled_reason");
        CopyIfPresent(payload, serviceData, "panel_kind");
        CopyIfPresent(payload, serviceData, "interaction_type");
        CopyIfPresent(payload, serviceData, "is_enabled");
        return payload;
    }

    private static void EnsureMemberId(GDictionary payload, StringName defaultMemberId)
    {
        if (!string.IsNullOrEmpty(ReadString(payload, "member_id")))
            return;
        if (SettlementActionRequest.IsEmpty(defaultMemberId))
            return;
        string memberId = SettlementActionRequest.ToText(defaultMemberId);
        payload["member_id"] = memberId;
        if (string.IsNullOrEmpty(ReadString(payload, "default_member_id")))
            payload["default_member_id"] = memberId;
    }

    private static bool IsSupportedModalSubmission(SettlementSubmissionSource source) =>
        source is
            SettlementSubmissionSource.ContractBoard
            or SettlementSubmissionSource.BountyBoard
            or SettlementSubmissionSource.Forge
            or SettlementSubmissionSource.NpcQuestOffer;

    private static void CopyIfPresent(
        GDictionary target,
        GDictionary source,
        string key
    )
    {
        if (
            target == null
            || source == null
            || string.IsNullOrEmpty(key)
            || !source.ContainsKey(key)
        )
        {
            return;
        }
        target[key] = source[key];
    }

    private static string ReadString(
        GDictionary source,
        string key,
        string fallback = ""
    )
    {
        if (source == null || !source.ContainsKey(key))
            return fallback ?? "";
        Variant value = source[key];
        // Boundary contract: only plain String is a valid submission value.
        // StringName Variants must be rejected so spoofed typed payloads
        // cannot pass as formal submission sources/ids.
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => fallback ?? "",
        };
    }

    private static StringName ReadStringName(GDictionary source, string key) =>
        new(ReadString(source, key));

    private static int ReadInt(GDictionary source, string key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }
}
