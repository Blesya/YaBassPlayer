namespace YamBassPlayer.Models
{
	public class Track
	{
		public string Title { get; }
		public string Artist { get; }
		public string Album { get; }
		public string Id { get; }

		public Track(string title, string artist, string album, string id)
		{
			Title = title;
			Artist = artist;
			Album = album;
			Id = id;
		}

		public override string ToString() => $"{Artist} — {Title}";
	}

}
