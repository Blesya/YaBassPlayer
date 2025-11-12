using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer
{
    internal class Program
    {
        private const string TOKEN = "y0__xDO1ZSLqveAAhje-AYg3e6yjxW4B8khQqZJ3_VP4V1ctUUSkrgV7A";


        static async Task Main(string[] args)
        {
            AuthStorage storage = new AuthStorage();
            YandexMusicApi api = new YandexMusicApi();

            api.User.Authorize(storage, TOKEN);
            
            // Получаем лайкнутые треки
            var likedTracksResponse = await api.Library.GetLikedTracksAsync(storage);
            
            if (likedTracksResponse?.Result?.Library?.Tracks != null)
            {
                var tracks = likedTracksResponse.Result.Library.Tracks;
                Console.WriteLine($"Всего треков в избранном: {tracks.Count}");
                Console.WriteLine("\nПервые 10 треков:\n");
                
                var tracksToShow = tracks.Take(10);
                int index = 1;
                
                foreach (var libraryTrack in tracksToShow)
                {
                    if (libraryTrack != null)
                    {
                        // Получаем полную информацию о треке
                        var trackResponse = await api.Track.GetAsync(storage, libraryTrack.Id);
                        var track = trackResponse?.Result?.FirstOrDefault();
                        
                        if (track != null)
                        {
                            string artists = track.Artists != null 
                                ? string.Join(", ", track.Artists.Select(a => a.Name))
                                : "Неизвестный исполнитель";
                            
                            Console.WriteLine($"{index}. {artists} - {track.Title}");
                            if (track.Albums != null && track.Albums.Any())
                            {
                                Console.WriteLine($"   Альбом: {track.Albums.First().Title}");
                            }
                            Console.WriteLine($"   ID: {track.Id}");
                            Console.WriteLine();
                        }
                    }
                    index++;
                }
            }
            else
            {
                Console.WriteLine("Не удалось получить избранные треки");
            }
        }
    }
}
