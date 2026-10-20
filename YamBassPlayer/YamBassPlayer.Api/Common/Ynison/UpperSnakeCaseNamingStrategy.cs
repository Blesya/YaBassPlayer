using Newtonsoft.Json.Serialization;

namespace YamBassPlayer.Api.Common.Ynison
{
    public class UpperSnakeCaseNamingStrategy: SnakeCaseNamingStrategy
    {
        protected override string ResolvePropertyName(string name) => base.ResolvePropertyName(name).ToUpper();
    }
}