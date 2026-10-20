namespace YamBassPlayer.Api.Models.Common
{
    /// <summary>
    /// Запись из глобального словаря дисклеймеров: текст возрастных/правовых
    /// ограничений и причин недоступности.
    /// </summary>
    public class YDisclaimer
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Reason { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
