using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.Tests.Services;

[TestFixture]
public sealed class QrCodeRendererTests
{
    private readonly QrCodeRenderer _renderer = new();

    private const string SampleLink = "https://passport.yandex.ru/auth/magic/code/?track_id=abc123";

    [Test]
    public void Render_ReturnsNonEmptyText()
    {
        string result = _renderer.Render(SampleLink);

        Assert.That(result, Is.Not.Empty);
        Assert.That(result.TrimEnd('\n'), Is.Not.Empty);
    }

    [Test]
    public void Render_UsesOnlyBlockCharactersAndSpaces()
    {
        string result = _renderer.Render(SampleLink);

        foreach (char c in result)
        {
            Assert.That(c, Is.AnyOf('█', '▀', '▄', ' ', '\n'),
                $"Неожиданный символ '{c}' в QR-коде");
        }
    }

    [Test]
    public void Render_ProducesRectangularOutput()
    {
        string result = _renderer.Render(SampleLink);
        string[] lines = result.TrimEnd('\n').Split('\n');

        Assert.That(lines.Select(l => l.Length).Distinct().Count(), Is.EqualTo(1),
            "Все строки QR-кода должны иметь одинаковую ширину");
    }

    [Test]
    public void Render_IsDeterministic()
    {
        string first = _renderer.Render(SampleLink);
        string second = _renderer.Render(SampleLink);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Render_DiffersForDifferentText()
    {
        Assert.That(_renderer.Render("https://example.com/a"),
            Is.Not.EqualTo(_renderer.Render("https://example.com/b")));
    }

    [Test]
    public void Render_EncodesLongLinkWithinReasonableSize()
    {
        string result = _renderer.Render(SampleLink);
        int width = result.Split('\n')[0].Length;

        // Ссылка входа укладывается в QR версии ~5: 37 модулей + тихая зона.
        Assert.That(width, Is.LessThanOrEqualTo(45));
    }
}
