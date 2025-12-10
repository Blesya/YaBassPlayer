using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;
using YamBassPlayer.Configuration;

namespace YamBassPlayer;

public static class Themes
{
    private const string DarkThemeName = "Dark";
    private const string LightThemeName = "Light";
    private const string MatrixThemeName = "Matrix";
    private const string CyberpunkThemeName = "Cyberpunk";
    private const string NordThemeName = "Nord";
    private const string DefaultThemeName = "Default";

    private static ColorScheme? _defaultBase;
	private static ColorScheme? _defaultDialog;

	public static void InitializeDefaults()
	{
		_defaultBase = Clone(Colors.Base);
		_defaultDialog = Clone(Colors.Dialog);
	}

	public static void ApplySavedTheme()
	{
		var themeName = AppConfiguration.GetTheme();
		if (string.IsNullOrEmpty(themeName))
        {
            return;
        }

		switch (themeName)
		{
			case DarkThemeName:
				ApplyDarkTheme(false);
				break;
			case LightThemeName:
				ApplyLightTheme(false);
				break;
			case MatrixThemeName:
				ApplyMatrixTheme(false);
				break;
			case CyberpunkThemeName:
				ApplyCyberpunkTheme(false);
				break;
			case NordThemeName:
				ApplyNordTheme(false);
				break;
			case DefaultThemeName:
				RestoreDefaultTheme(false);
				break;
		}
	}

	public static void ApplyDarkTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(DarkThemeName);
        }

		Application.Refresh();
	}

	public static void ApplyLightTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(LightThemeName);
        }

		Application.Refresh();
	}

	public static void ApplyMatrixTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(MatrixThemeName);
        }

		Application.Refresh();
	}

	public static void ApplyCyberpunkTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(CyberpunkThemeName);
        }

		Application.Refresh();
	}

	public static void ApplyNordTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(NordThemeName);
        }

		Application.Refresh();
	}

	public static void RestoreDefaultTheme(bool save = true)
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

		if (save)
        {
            AppConfiguration.SaveTheme(DefaultThemeName);
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