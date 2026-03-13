namespace YamBassPlayer.Models;

public record GraphEdge(string FromId, string ToId, int Weight);

public class GraphData(
	string centerTrackId,
	IReadOnlyList<GraphEdge> edges,
	IReadOnlyDictionary<string, string> trackLabels)
{
	public string CenterTrackId { get; } = centerTrackId;
	public IReadOnlyList<GraphEdge> Edges { get; } = edges;
	public IReadOnlyDictionary<string, string> TrackLabels { get; } = trackLabels;
}
