using Newtonsoft.Json;

using YamBassPlayer.Api.Models.Common;
using YamBassPlayer.Api.Models.Radio;

namespace YamBassPlayer.Tests.Models;

[TestFixture]
public class RadioSequenceModelsTests
{
    [Test]
    public void StationSequence_WithFloatHue_Deserializes()
    {
        const string json = """
        {
          "result": {
            "batchId": "batch-1",
            "sequence": [
              {
                "type": "music",
                "track": { "id": "123", "title": "Track" },
                "trackParameters": { "bpm": 120, "hue": 23.0, "energy": 0.5 }
              }
            ]
          }
        }
        """;

        var response = JsonConvert.DeserializeObject<YResponse<YStationSequence>>(json);

        Assert.Multiple(() =>
        {
            Assert.That(response!.Result.Sequence, Has.Count.EqualTo(1));
            Assert.That(response.Result.Sequence[0].TrackParameters.Hue, Is.EqualTo(23.0));
        });
    }
}
