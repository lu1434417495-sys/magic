#nullable enable

using System;
using System.Buffers;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

internal static class SkillGenerationValidationProtocol
{
    internal const string ProtocolId = "magic.skill_generation.validation/v1";

    internal static string FormatJson(SkillGenerationValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return WriteJson(indented: true, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", ProtocolId);
            writer.WriteBoolean("success", report.Success);
            writer.WriteNumber("exit_code", report.ExitCode);
            if (report.RejectedStage.HasValue)
            {
                writer.WriteString(
                    "rejected_stage",
                    SkillGenerationValidationStageCodec.ToWireValue(
                        report.RejectedStage.Value
                    )
                );
            }
            else
            {
                writer.WriteNull("rejected_stage");
            }
            writer.WritePropertyName("stages");
            writer.WriteStartArray();
            foreach (SkillGenerationValidationStageReport stage in report.Stages)
                WriteStage(writer, stage);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    internal static string FormatNdjson(SkillGenerationValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var output = new StringBuilder();
        foreach (SkillGenerationValidationStageReport stage in report.Stages)
        {
            foreach (ContentJsonDiagnostic diagnostic in stage.Diagnostics)
            {
                output.Append(WriteJson(indented: false, writer =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "diagnostic");
                    writer.WriteString("protocol", ProtocolId);
                    WriteDiagnosticFields(writer, stage.Stage, diagnostic);
                    writer.WriteEndObject();
                }));
            }
            output.Append(WriteJson(indented: false, writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "stage_summary");
                writer.WriteString("protocol", ProtocolId);
                WriteStageSummaryFields(writer, stage);
                writer.WriteEndObject();
            }));
        }
        output.Append(WriteJson(indented: false, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "summary");
            writer.WriteString("protocol", ProtocolId);
            writer.WriteBoolean("success", report.Success);
            writer.WriteNumber("exit_code", report.ExitCode);
            if (report.RejectedStage.HasValue)
            {
                writer.WriteString(
                    "rejected_stage",
                    SkillGenerationValidationStageCodec.ToWireValue(
                        report.RejectedStage.Value
                    )
                );
            }
            else
            {
                writer.WriteNull("rejected_stage");
            }
            writer.WriteEndObject();
        }));
        return output.ToString();
    }

    private static void WriteStage(
        Utf8JsonWriter writer,
        SkillGenerationValidationStageReport stage
    )
    {
        writer.WriteStartObject();
        WriteStageSummaryFields(writer, stage);
        writer.WritePropertyName("diagnostics");
        writer.WriteStartArray();
        foreach (ContentJsonDiagnostic diagnostic in stage.Diagnostics)
        {
            writer.WriteStartObject();
            WriteDiagnosticFields(writer, stage.Stage, diagnostic);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStageSummaryFields(
        Utf8JsonWriter writer,
        SkillGenerationValidationStageReport stage
    )
    {
        writer.WriteString(
            "stage",
            SkillGenerationValidationStageCodec.ToWireValue(stage.Stage)
        );
        writer.WriteBoolean("success", stage.Success);
        writer.WriteNumber("validated_entry_count", stage.ValidatedEntryCount);
        writer.WriteNumber("diagnostic_count", stage.Diagnostics.Count);
        writer.WritePropertyName("metrics");
        writer.WriteStartObject();
        foreach ((string key, object value) in stage.Metrics.OrderBy(
            pair => pair.Key,
            StringComparer.Ordinal
        ))
        {
            WriteMetric(writer, key, value);
        }
        writer.WriteEndObject();
    }

    private static void WriteDiagnosticFields(
        Utf8JsonWriter writer,
        SkillGenerationValidationStageKind stage,
        ContentJsonDiagnostic diagnostic
    )
    {
        writer.WriteString("stage", SkillGenerationValidationStageCodec.ToWireValue(stage));
        writer.WriteString("source_label", diagnostic.SourceLabel ?? "");
        writer.WriteString("json_pointer", diagnostic.JsonPointer ?? "");
        writer.WriteString("rule_id", diagnostic.RuleId ?? "");
        writer.WriteString("expected", diagnostic.Expected ?? "");
        writer.WriteString("actual", diagnostic.Actual ?? "");
        writer.WriteString("message", diagnostic.Message ?? "");
    }

    private static void WriteMetric(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(key);
                break;
            case bool boolean:
                writer.WriteBoolean(key, boolean);
                break;
            case byte number:
                writer.WriteNumber(key, number);
                break;
            case short number:
                writer.WriteNumber(key, number);
                break;
            case int number:
                writer.WriteNumber(key, number);
                break;
            case long number:
                writer.WriteNumber(key, number);
                break;
            case float number:
                writer.WriteNumber(key, number);
                break;
            case double number:
                writer.WriteNumber(key, number);
                break;
            case decimal number:
                writer.WriteNumber(key, number);
                break;
            case string text:
                writer.WriteString(key, text);
                break;
            default:
                writer.WriteString(
                    key,
                    Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                );
                break;
        }
    }

    private static string WriteJson(bool indented, Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Indented = indented,
                    SkipValidation = false,
                }
            )
        )
        {
            write(writer);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
            + "\n";
    }
}
