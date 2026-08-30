using System.Text;
using System.Text.Json;
using Game.Sim.Entities;
using Game.Sim.Events;

namespace Game.Sim.Logging;

public sealed class JsonlWorldEventLogger : IWorldEventLogger
{
    private const int SchemaVersion = 1;
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false,
    };

    private readonly TextWriter _output;

    public JsonlWorldEventLogger(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public void Write(WorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", SchemaVersion);
            json.WriteNumber("id", worldEvent.Id.Value);
            json.WriteNumber("tick", worldEvent.Time.Tick);
            json.WriteString("type", worldEvent.Type.ToString());
            json.WriteString("actor", worldEvent.Actor.Value);

            if (worldEvent.Target is EntityId target)
            {
                json.WriteString("target", target.Value);
            }
            else
            {
                json.WriteNull("target");
            }

            json.WriteString("location", worldEvent.Location.Value);
            json.WriteStartArray("tags");
            foreach (EventTag tag in worldEvent.Tags)
            {
                json.WriteStringValue(tag.ToString());
            }

            json.WriteEndArray();
            WritePayload(json, worldEvent.Payload);
            json.WriteEndObject();
        }

        _output.Write(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length)));
        _output.Write('\n');
    }

    public void Flush() => _output.Flush();

    private static void WritePayload(Utf8JsonWriter json, EventPayload payload)
    {
        json.WriteStartObject("payload");

        switch (payload)
        {
            case EmptyEventPayload:
                json.WriteString("type", "empty");
                break;
            case LocationTransitionPayload transition:
                json.WriteString("type", "locationTransition");
                json.WriteString("origin", transition.Origin.Value);
                json.WriteString("destination", transition.Destination.Value);
                break;
            case SecretActivityPayload secret:
                json.WriteString("type", "secretActivity");
                json.WriteString("planId", secret.PlanId);
                break;
            case InteractionPayload interaction:
                json.WriteString("type", "interaction");
                json.WriteString("kind", interaction.Kind.ToString());
                json.WriteString("interactionId", interaction.InteractionId);
                break;
            case RoleDutyPayload roleDuty:
                json.WriteString("type", "roleDuty");
                json.WriteString("dutyId", roleDuty.DutyId);
                break;
            case BoundaryProbePayload boundaryProbe:
                json.WriteString("type", "boundaryProbe");
                json.WriteString("boundaryId", boundaryProbe.BoundaryId);
                break;
            case BehaviorPatternPayload pattern:
                json.WriteString("type", "behaviorPattern");
                json.WriteString("pattern", pattern.Pattern.ToString());
                json.WriteStartArray("evidenceEvents");
                foreach (EventId evidenceEvent in pattern.EvidenceEvents)
                {
                    json.WriteNumberValue(evidenceEvent.Value);
                }

                json.WriteEndArray();
                break;
            default:
                throw new NotSupportedException(
                    $"Payload type '{payload.GetType().Name}' is not supported by the JSONL logger.");
        }

        json.WriteEndObject();
    }
}
