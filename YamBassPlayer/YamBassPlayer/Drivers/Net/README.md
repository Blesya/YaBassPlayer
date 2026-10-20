# NetDriver из Terminal.Gui v1.19.0

Вендоренная копия ANSI-драйвера Terminal.Gui, потому что `NetDriver`,
`NetMainLoop` и `EscSeqUtils` в пакете `internal` и унаследовать их нельзя.

Источник: https://github.com/gui-cs/Terminal.Gui/tree/v1.19.0
Лицензия: MIT (Copyright (c) 2017 Miguel de Icaza).

Что изменено относительно upstream:

- `NetDriver.cs`: 24-битный цвет — параллельный буфер `RgbCell[,]`,
  `ITrueColorDriver.SetRgbAttribute/ResetRgbAttribute`, запись RGB в `AddRune`,
  вывод `ESC[38;2;r;g;bm` / `ESC[48;2;r;g;bm` в `UpdateScreen`.
  Остальные клетки по-прежнему рисуются 16-цветным SGR.
- `NetDriver.cs`: clipboard-ветка оставлена только для Windows
  (`WindowsClipboard`), `Platform.Suspend()` вырезан, `mainLoop.timeouts` и
  `mainLoop.idleHandlers` заменены на публичные `Timeouts`/`IdleHandlers`.
- `NetDriver.cs`: `Queue<T>` для ввода (`NetEvents.inputResultQueue` и
  `NetMainLoop.inputResult`) заменены на `ConcurrentQueue<T>`: очередь читают и
  пишут разные потоки, из-за чего приложение падало с
  `InvalidOperationException: Queue empty` и вход «зависал».
- `WindowsClipboard.cs` вынесен из `WindowsDriver.cs` (Windows-only).

Namespace `Terminal.Gui` сохранён намеренно: так патч остаётся минимальным,
а типы остаются `internal` и не конфликтуют с пакетом.

При обновлении Terminal.Gui эти файлы нужно пересобрать из нового тега и
заново применить перечисленные правки.
