using Microsoft.ML;
using Microsoft.ML.Data;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Фасад для работы с сохранённой ML-моделью.
/// Загружает next_model.zip + карту ключей (next_key_map.txt) и даёт простой метод:
/// по (prev, current) -> следующий trackId (строка).
/// </summary>
public sealed class NextTrackPredictor : INextTrackPredictor
{
	private readonly MLContext _mlContext = new();
	private readonly string _modelPath;
	private readonly string _keyMapPath;
	private PredictionEngine<InputRow, OutputRow>? _engine;
	private Dictionary<uint, string>? _keyToTrack; // PredictedLabel (Key-индекс) -> trackId (string)

	public NextTrackPredictor(string modelPath, string keyMapPath)
	{
		_modelPath = modelPath;
		_keyMapPath = keyMapPath;
	}

	public bool IsReady
	{
		get
		{
			TryEnsureLoaded();
			return _engine is not null;
		}
	}

	/// <summary>
	/// Главный метод фасада: current -> prev -> prev2 (цепочка от самого свежего трека к более раннему).
	/// Возвращает рекомендацию следующего трека; при неудаче в Message — причина.
	/// </summary>
	public NextTrackRecommendation GetNext(string prev, string current, string prev2)
	{
		Logging.LogBeforeCall();

		var notLoaded = !TryEnsureLoaded();
		if (notLoaded || _engine is null || _keyToTrack is null)
		{
			string modelState = File.Exists(_modelPath) && File.Exists(_keyMapPath)
				? $"модель загружена ({Path.GetFileName(_modelPath)}), карта ключей загружена"
				: BuildMissingFilesInfo();
			return new NextTrackRecommendation
			{
				Message = $"Модель рекомендаций недоступна.{Environment.NewLine}" +
				          $"Путь к модели: {_modelPath}{Environment.NewLine}" +
				          $"Путь к карте ключей: {_keyMapPath}{Environment.NewLine}" +
				          modelState
			};
		}

		var input = new InputRow { Prev1 = prev, Current = current, Prev2 = prev2, Next = "" };
		OutputRow? prediction = _engine.Predict(input);

		//
        uint[] topIndexes = GetTopIndexes(prediction, 5);

        // Случайный выбор из топ-5 предсказанных классов (Key-индексов).
        uint random = topIndexes[new Random().Next(topIndexes.Length)];

		// PredictedLabel — это Key-индекс (uint), декодируем в строковый trackId по карте.
		if (_keyToTrack.TryGetValue(random, out var trackId))
		{
			Logging.LogAfterCall();
			return new NextTrackRecommendation
			{
				TrackId = trackId,
				Message = $"Рекомендован трек: {trackId}"
			};
		}

		return new NextTrackRecommendation
		{
			Message = "Модель не смогла рекомендовать следующий трек: " +
			          $"предсказанный класс (Key-индекс {prediction.PredictedLabel}) отсутствует в карте ключей." +
			          Environment.NewLine +
			          $"Контекст: prev = «{prev}», current = «{current}»." +
			          Environment.NewLine +
			          "Проверьте, что next_model.zip и next_key_map.txt получены из одной и той же тренировки."
		};
	}

    private uint[] GetTopIndexes(OutputRow prediction, int n)
    {
        return prediction.Scores
            .Select((score, index) => new { Score = score, Index = (uint)index + 1 })
            .OrderByDescending(x => x.Score)
            .Take(n)
            .Select(x => x.Index)
            .ToArray();
    }

    private bool TryEnsureLoaded()
	{
		if (_engine is not null)
			return true;

		if (!File.Exists(_modelPath) || !File.Exists(_keyMapPath))
			return false;

		try
		{
			// Загружаем обученную модель (цепочку трансформов + трейнер).
			var loaded = _mlContext.Model.Load(_modelPath, out _);
			_engine = _mlContext.Model.CreatePredictionEngine<InputRow, OutputRow>(loaded);

			// Загружаем карту: PredictedLabel (Key-индекс uint) -> исходный trackId (string).
			_keyToTrack = new Dictionary<uint, string>();
			foreach (var line in File.ReadAllLines(_keyMapPath))
			{
				var parts = line.Split('\t');
				if (parts.Length < 2 || !uint.TryParse(parts[0], out var key))
					continue;
				_keyToTrack[key] = parts[1];
			}

			return true;
		}
		catch
		{
			_engine = null;
			return false;
		}
	}

	private string BuildMissingFilesInfo()
	{
		var missing = new List<string>();
		if (!File.Exists(_modelPath)) missing.Add($"нет файла модели: {_modelPath}");
		if (!File.Exists(_keyMapPath)) missing.Add($"нет файла карты ключей: {_keyMapPath}");
		return missing.Count == 0 ? "файлы на месте" : string.Join(Environment.NewLine, "Отсутствуют файлы:", missing);
	}

	// Входная модель данных: свойства = колонки, которые ожидает pipeline.
	// Prev1/Prev2/Current — признаки (переходы: какой трек был ранее), Current — текущий,
	// Next — заглушка (колонка обязана быть в схеме, модель её не использует).
	private class InputRow
	{
		public string? Prev1 { get; set; }
		public string? Prev2 { get; set; }
		public string? Current { get; set; }
		public string? Next { get; set; }
	}

	// Выходная модель данных: свойства = колонки, которые производит модель.
	// PredictedLabel — Key<UInt32> (только число), декодируем в строку по карте,
	// Score — вектор вероятностей по всем классам.
	private class OutputRow
	{
		public uint PredictedLabel { get; set; }

		[ColumnName("Score")]
		public float[] Scores { get; set; } = Array.Empty<float>();
	}
}
