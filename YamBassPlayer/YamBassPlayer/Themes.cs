using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace YamBassPlayer;

public static class Themes
{
    private static ColorScheme? _defaultBase;
    private static ColorScheme? _defaultDialog;

    public static void InitializeDefaults()
    {
        _defaultBase = Clone(Colors.Base);
        _defaultDialog = Clone(Colors.Dialog);
    }

    public static void ApplyDarkTheme()
    {
        var b = Colors.Base;
        b.Normal = new Attribute(Color.White, Color.Black);
        b.Focus = new Attribute(Color.Black, Color.Gray);
        b.HotNormal = new Attribute(Color.BrightCyan, Color.Black);
        b.HotFocus = new Attribute(Color.Black, Color.BrightCyan);

        var d = Colors.Dialog;
        d.Normal = new Attribute(Color.Black, Color.Gray);
        d.Focus = new Attribute(Color.Black, Color.White);
        d.HotNormal = new Attribute(Color.BrightBlue, Color.Gray);
        d.HotFocus = new Attribute(Color.Black, Color.BrightBlue);

        Application.Refresh();
    }

    public static void ApplyLightTheme()
    {
        var b = Colors.Base;
        b.Normal = new Attribute(Color.Black, Color.Gray);
        b.Focus = new Attribute(Color.White, Color.DarkGray);
        b.HotNormal = new Attribute(Color.Blue, Color.Gray);
        b.HotFocus = new Attribute(Color.White, Color.Blue);

        var d = Colors.Dialog;
        d.Normal = new Attribute(Color.Black, Color.DarkGray);
        d.Focus = new Attribute(Color.White, Color.Black);
        d.HotNormal = new Attribute(Color.Blue, Color.DarkGray);
        d.HotFocus = new Attribute(Color.White, Color.Blue);

        Application.Refresh();
    }

    public static void ApplyMatrixTheme()
    {
        var b = Colors.Base;
        b.Normal = new Attribute(Color.BrightGreen, Color.Black);
        b.Focus = new Attribute(Color.Black, Color.BrightGreen);
        b.HotNormal = new Attribute(Color.Green, Color.Black);
        b.HotFocus = new Attribute(Color.Black, Color.Green);

        var d = Colors.Dialog;
        d.Normal = new Attribute(Color.BrightGreen, Color.Black);
        d.Focus = new Attribute(Color.Black, Color.BrightGreen);
        d.HotNormal = new Attribute(Color.Green, Color.Black);
        d.HotFocus = new Attribute(Color.Black, Color.Green);

        Application.Refresh();
    }

    public static void ApplyCyberpunkTheme()
    {
        var b = Colors.Base;
        b.Normal = new Attribute(Color.BrightMagenta, Color.Black);
        b.Focus = new Attribute(Color.Black, Color.BrightMagenta);
        b.HotNormal = new Attribute(Color.BrightYellow, Color.Black);
        b.HotFocus = new Attribute(Color.Black, Color.BrightYellow);

        var d = Colors.Dialog;
        d.Normal = new Attribute(Color.BrightMagenta, Color.DarkGray);
        d.Focus = new Attribute(Color.White, Color.Black);
        d.HotNormal = new Attribute(Color.BrightYellow, Color.DarkGray);
        d.HotFocus = new Attribute(Color.Black, Color.BrightYellow);

        Application.Refresh();
    }

    public static void ApplyNordTheme()
    {
        var b = Colors.Base;
        b.Normal = new Attribute(Color.BrightCyan, Color.Black);
        b.Focus = new Attribute(Color.White, Color.Blue);
        b.HotNormal = new Attribute(Color.Cyan, Color.Black);
        b.HotFocus = new Attribute(Color.Black, Color.BrightCyan);

        var d = Colors.Dialog;
        d.Normal = new Attribute(Color.White, Color.DarkGray);
        d.Focus = new Attribute(Color.Black, Color.BrightBlue);
        d.HotNormal = new Attribute(Color.BrightCyan, Color.DarkGray);
        d.HotFocus = new Attribute(Color.Black, Color.Cyan);

        Colors.Menu = new ColorScheme
        {
            Normal = new Attribute(Color.White, Color.Blue),
            Focus = new Attribute(Color.Black, Color.BrightBlue),
            HotNormal = new Attribute(Color.Cyan, Color.Blue),
            HotFocus = new Attribute(Color.Black, Color.Cyan)
        };

        Colors.Error = new ColorScheme
        {
            Normal = new Attribute(Color.White, Color.Red),
            Focus = new Attribute(Color.White, Color.BrightRed),
            HotNormal = new Attribute(Color.BrightYellow, Color.Red),
            HotFocus = new Attribute(Color.Black, Color.BrightYellow)
        };

        Application.Refresh();
    }

    public static void RestoreDefaultTheme()
    {
        if (_defaultBase != null)
        {
            Colors.Base.Normal = _defaultBase.Normal;
            Colors.Base.Focus = _defaultBase.Focus;
            Colors.Base.HotNormal = _defaultBase.HotNormal;
            Colors.Base.HotFocus = _defaultBase.HotFocus;
        }

        if (_defaultDialog != null)
        {
            Colors.Dialog.Normal = _defaultDialog.Normal;
            Colors.Dialog.Focus = _defaultDialog.Focus;
            Colors.Dialog.HotNormal = _defaultDialog.HotNormal;
            Colors.Dialog.HotFocus = _defaultDialog.HotFocus;
        }

        Application.Refresh();
    }

    private static ColorScheme Clone(ColorScheme scheme)
    {
        return new ColorScheme
        {
            Normal = scheme.Normal,
            Focus = scheme.Focus,
            HotNormal = scheme.HotNormal,
            HotFocus = scheme.HotFocus
        };
    }
}