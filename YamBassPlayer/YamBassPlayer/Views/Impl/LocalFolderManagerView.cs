using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class LocalFolderManagerView : Dialog, ILocalFolderManagerView
{
	private readonly ListView _foldersListView;
	private readonly Label _statusLabel;
	private List<LocalFolder> _folders = [];

	public event Action? OnAddFolderClicked;
	public event Action<int>? OnRemoveFolderClicked;
	public event Action<int>? OnScanFolderClicked;
	public event Action? OnScanAllClicked;
	public event Action? OnCloseClicked;

	public LocalFolderManagerView() : base("Управление локальными папками")
	{
		Width = 80;
		Height = 24;

		var foldersLabel = new Label
		{
			X = 1,
			Y = 1,
			Width = Dim.Fill(1),
			Text = "Локальные папки:"
		};

		_foldersListView = new ListView
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill(1),
			Height = Dim.Fill(7),
			AllowsMarking = false
		};

		_statusLabel = new Label
		{
			X = 1,
			Y = Pos.AnchorEnd(5),
			Width = Dim.Fill(1),
			Text = string.Empty
		};

		// First button row: Add, Remove, Scan Folder
		var addButton = new Button("Добавить папку")
		{
			X = 1,
			Y = Pos.AnchorEnd(3)
		};
		addButton.Clicked += () => OnAddFolderClicked?.Invoke();

		var removeButton = new Button("Удалить")
		{
			X = Pos.Right(addButton) + 1,
			Y = Pos.AnchorEnd(3)
		};
		removeButton.Clicked += HandleRemoveClicked;

		var scanFolderButton = new Button("Сканировать папку")
		{
			X = Pos.Right(removeButton) + 1,
			Y = Pos.AnchorEnd(3)
		};
		scanFolderButton.Clicked += HandleScanFolderClicked;

		// Second button row: Scan All, Close
		var scanAllButton = new Button("Сканировать всё")
		{
			X = 1,
			Y = Pos.AnchorEnd(1)
		};
		scanAllButton.Clicked += () => OnScanAllClicked?.Invoke();

		var closeButton = new Button("Закрыть")
		{
			X = Pos.AnchorEnd(13),
			Y = Pos.AnchorEnd(1)
		};
		closeButton.Clicked += () => OnCloseClicked?.Invoke();

		Add(foldersLabel, _foldersListView, _statusLabel,
			addButton, removeButton, scanFolderButton,
			scanAllButton, closeButton);
	}

	/// <inheritdoc/>
	public void SetFolders(IReadOnlyList<LocalFolder> folders)
	{
		_folders = folders.ToList();

		var displayList = _folders
			.Select(f =>
			{
				string scanned = f.LastScannedAt.HasValue
					? f.LastScannedAt.Value.LocalDateTime.ToString("g")
					: "не сканировалась";
				return $"{f.Name}  [{scanned}]  {f.Path}";
			})
			.ToList();

		_foldersListView.SetSource(displayList);
	}

	/// <inheritdoc/>
	public void ShowScanProgress(string message)
	{
		_statusLabel.Text = message;
		_statusLabel.SetNeedsDisplay();
	}

	/// <inheritdoc/>
	public void ShowScanCompleted(int newTracksCount)
	{
		_statusLabel.Text = $"Сканирование завершено. Новых треков: {newTracksCount}";
		_statusLabel.SetNeedsDisplay();
	}

	/// <inheritdoc/>
	public void Show() => Application.Run(this);

	/// <inheritdoc/>
	public void Close() => Application.RequestStop(this);

	private void HandleRemoveClicked()
	{
		int idx = _foldersListView.SelectedItem;
		if (idx < 0 || idx >= _folders.Count)
		{
			MessageBox.ErrorQuery("Ошибка", "Выберите папку для удаления.", "OK");
			return;
		}

		OnRemoveFolderClicked?.Invoke(_folders[idx].Id);
	}

	private void HandleScanFolderClicked()
	{
		int idx = _foldersListView.SelectedItem;
		if (idx < 0 || idx >= _folders.Count)
		{
			MessageBox.ErrorQuery("Ошибка", "Выберите папку для сканирования.", "OK");
			return;
		}

		OnScanFolderClicked?.Invoke(_folders[idx].Id);
	}
}
