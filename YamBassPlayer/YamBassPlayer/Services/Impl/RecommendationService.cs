using Microsoft.Data.Sqlite;
using YamBassPlayer.Configuration;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public class RecommendationService(SqliteConnection connection, ITrackInfoProvider trackInfoProvider)
: IRecommendationService
{
private static readonly TimeSpan RecentThreshold = TimeSpan.FromMinutes(30);
private static readonly TimeSpan RecentlyPlayedCacheTtl = TimeSpan.FromSeconds(30);
private const double DirectWeight = 3.0;
private const double ReverseWeight = 1.0;
private const double ArtistTransitionWeight = 1.5;
private const double SingleCountPenalty = 0.5;
private const double RecentPenalty = 0.7;
private const double ArtistCapPercent = 0.4;

private Dictionary<string, Dictionary<string, int>>? _cachedGraph;
private int _lastHistoryCount = -1;
private HashSet<string>? _cachedRecentlyPlayed;
private DateTime _recentlyPlayedCacheTime = DateTime.MinValue;

public async Task<RecommendationResult> GetRecommendationsAsync(string currentTrackId, int limit = 20)
{
var sessionGap = TimeSpan.FromMinutes(AppConfiguration.GetSessionGapMinutes());
var transitions = GetOrBuildGraph(sessionGap);

var currentTrack = await trackInfoProvider.GetTrackInfoById(currentTrackId);
string? currentArtist = currentTrack?.Artist;

int directCount = transitions.TryGetValue(currentTrackId, out var direct) ? direct.Count : 0;
bool usedArtistFallback = directCount < 3;

HashSet<string> artistTrackIds = new();
if (currentArtist != null)
{
var ids = await trackInfoProvider.GetTrackIdsByArtistAsync(currentArtist);
artistTrackIds = new HashSet<string>(ids);
artistTrackIds.Remove(currentTrackId);
}

var candidates = new Dictionary<string, (double score, RecommendationReason reason)>();

if (transitions.TryGetValue(currentTrackId, out var directTransitions))
{
foreach (var (target, count) in directTransitions)
{
if (target == currentTrackId) continue;
var newScore = count * DirectWeight;
candidates[target] = (newScore, new RecommendationReason(RecommendationReasonType.DirectTransition, count));
}
}

foreach (var (source, targets) in transitions)
{
if (targets.TryGetValue(currentTrackId, out int reverseCount))
{
if (source == currentTrackId) continue;
if (candidates.TryGetValue(source, out var existing))
{
candidates[source] = (existing.score + reverseCount * ReverseWeight, existing.reason);
}
else
{
candidates[source] = (reverseCount * ReverseWeight, new RecommendationReason(RecommendationReasonType.ReverseTransition, reverseCount));
}
}
}

foreach (string artistTrackId in artistTrackIds)
{
if (!transitions.TryGetValue(artistTrackId, out var artistTargets)) continue;
foreach (var (target, count) in artistTargets)
{
if (target == currentTrackId) continue;
if (candidates.TryGetValue(target, out var existing))
{
candidates[target] = (existing.score + count * ArtistTransitionWeight, existing.reason);
}
else
{
candidates[target] = (count * ArtistTransitionWeight, new RecommendationReason(RecommendationReasonType.ArtistTransition, count));
}
}
}

if (candidates.Count < 3 && usedArtistFallback)
{
return new RecommendationResult(currentTrackId, [], true, true);
}

if (candidates.Count < 3)
{
return new RecommendationResult(currentTrackId, [], true, usedArtistFallback);
}

ApplySingleCountPenalty(candidates, transitions, currentTrackId, artistTrackIds);

var recentlyPlayed = GetRecentlyPlayedCached();
ApplyRecentPenalty(candidates, recentlyPlayed);

var allCandidateIds = candidates.Keys.ToList();
var trackInfos = (await trackInfoProvider.GetTracksInfoByIds(allCandidateIds)).ToDictionary(t => t.Id);

var validCandidates = candidates
.Where(c => trackInfos.ContainsKey(c.Key))
.OrderByDescending(c => c.Value.score)
.ToList();

int artistCap = (int)(limit * ArtistCapPercent);
var artistCounts = new Dictionary<string, int>();
var result = new List<RecommendedTrack>();

foreach (var (trackId, (score, reason)) in validCandidates)
{
if (result.Count >= limit) break;

string artist = trackInfos[trackId].Artist;
artistCounts.TryGetValue(artist, out int currentCount);

if (currentCount >= artistCap) continue;

result.Add(new RecommendedTrack(trackId, score, reason));
artistCounts[artist] = currentCount + 1;
}

return new RecommendationResult(currentTrackId, result, false, usedArtistFallback);
}

private Dictionary<string, Dictionary<string, int>> GetOrBuildGraph(TimeSpan sessionGap)
{
int currentCount = GetHistoryCount();
if (currentCount == _lastHistoryCount && _cachedGraph != null)
{
return _cachedGraph;
}

var history = LoadHistory();
_cachedGraph = BuildTransitionGraph(history, sessionGap);
_lastHistoryCount = currentCount;
return _cachedGraph;
}

private int GetHistoryCount()
{
using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM listensHistory WHERE source = 'Regular'";
return Convert.ToInt32(cmd.ExecuteScalar());
}

private List<(string trackId, DateTime utcTime)> LoadHistory()
{
var result = new List<(string trackId, DateTime utcTime)>();

using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT trackId, utcTime FROM listensHistory WHERE source = 'Regular' ORDER BY utcTime";

using var reader = cmd.ExecuteReader();
while (reader.Read())
{
string trackId = reader.GetString(0);
string utcTimeStr = reader.GetString(1);
if (DateTime.TryParse(utcTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utcTime))
{
result.Add((trackId, utcTime));
}
}

return result;
}

private static Dictionary<string, Dictionary<string, int>> BuildTransitionGraph(
List<(string trackId, DateTime utcTime)> history,
TimeSpan sessionGap)
{
var transitions = new Dictionary<string, Dictionary<string, int>>();

for (int i = 0; i < history.Count - 1; i++)
{
var current = history[i];
var next = history[i + 1];

if (next.utcTime - current.utcTime > sessionGap) continue;

if (!transitions.TryGetValue(current.trackId, out var targets))
{
targets = new Dictionary<string, int>();
transitions[current.trackId] = targets;
}

targets.TryGetValue(next.trackId, out int count);
targets[next.trackId] = count + 1;
}

return transitions;
}

private static void ApplySingleCountPenalty(
Dictionary<string, (double score, RecommendationReason reason)> candidates,
Dictionary<string, Dictionary<string, int>> transitions,
string currentTrackId,
HashSet<string> artistTrackIds)
{
foreach (var trackId in candidates.Keys.ToList())
{
bool allSingle = true;

if (transitions.TryGetValue(currentTrackId, out var direct) && direct.TryGetValue(trackId, out int dc) && dc > 1)
allSingle = false;

if (allSingle)
{
foreach (var (source, targets) in transitions)
{
if (targets.TryGetValue(currentTrackId, out int rc) && source == trackId && rc > 1)
{
allSingle = false;
break;
}
}
}

if (allSingle)
{
foreach (string artistTrackId in artistTrackIds)
{
if (transitions.TryGetValue(artistTrackId, out var at) && at.TryGetValue(trackId, out int ac) && ac > 1)
{
allSingle = false;
break;
}
}
}

if (allSingle)
{
var (score, reason) = candidates[trackId];
candidates[trackId] = (score * SingleCountPenalty, reason);
}
}
}

private HashSet<string> GetRecentlyPlayedCached()
{
if (_cachedRecentlyPlayed != null && DateTime.UtcNow - _recentlyPlayedCacheTime < RecentlyPlayedCacheTtl)
{
return _cachedRecentlyPlayed;
}

_cachedRecentlyPlayed = GetRecentlyPlayedTrackIds();
_recentlyPlayedCacheTime = DateTime.UtcNow;
return _cachedRecentlyPlayed;
}

private HashSet<string> GetRecentlyPlayedTrackIds()
{
var result = new HashSet<string>();
var threshold = DateTime.UtcNow - RecentThreshold;

using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT DISTINCT trackId FROM listensHistory WHERE source = 'Regular' AND utcTime >= $threshold";
cmd.Parameters.AddWithValue("$threshold", threshold.ToString("O"));

using var reader = cmd.ExecuteReader();
while (reader.Read())
{
result.Add(reader.GetString(0));
}

return result;
}

private static void ApplyRecentPenalty(
Dictionary<string, (double score, RecommendationReason reason)> candidates,
HashSet<string> recentlyPlayed)
{
foreach (var trackId in candidates.Keys.ToList())
{
if (recentlyPlayed.Contains(trackId))
{
var (score, reason) = candidates[trackId];
candidates[trackId] = (score * RecentPenalty, reason);
}
}
}

public async Task<GraphData> GetGraphDataAsync(string centerTrackId, int depth = 2, int maxEdgesPerNode = 5)
{
var sessionGap = TimeSpan.FromMinutes(AppConfiguration.GetSessionGapMinutes());
var transitions = GetOrBuildGraph(sessionGap);

var edges = new List<GraphEdge>();
var visitedNodes = new HashSet<string> { centerTrackId };
var queue = new Queue<(string nodeId, int currentDepth)>();
queue.Enqueue((centerTrackId, 0));

while (queue.Count > 0)
{
var (nodeId, currentDepth) = queue.Dequeue();
if (currentDepth >= depth) continue;

if (transitions.TryGetValue(nodeId, out var outgoing))
{
var topOutgoing = outgoing
.OrderByDescending(kv => kv.Value)
.Take(maxEdgesPerNode);

foreach (var (target, weight) in topOutgoing)
{
edges.Add(new GraphEdge(nodeId, target, weight));
if (visitedNodes.Add(target))
queue.Enqueue((target, currentDepth + 1));
}
}

foreach (var (source, targets) in transitions)
{
if (!targets.TryGetValue(nodeId, out int reverseWeight)) continue;
if (currentDepth == 0)
{
edges.Add(new GraphEdge(source, nodeId, reverseWeight));
if (visitedNodes.Add(source))
queue.Enqueue((source, currentDepth + 1));
}
}
}

var allNodeIds = visitedNodes.ToList();
var trackInfos = await trackInfoProvider.GetTracksInfoByIds(allNodeIds);
var labels = trackInfos.ToDictionary(
t => t.Id,
t => $"{t.Artist} — {t.Title}");

foreach (var id in allNodeIds.Where(id => !labels.ContainsKey(id)))
labels[id] = id;

return new GraphData(centerTrackId, edges, labels);
}
}
