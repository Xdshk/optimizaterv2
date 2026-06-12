# Changelog

## [2.0.0] — 2026-06-12

### Полностью переработанное приложение

#### Исправления критических багов
- **PerformanceMonitor** — методы `GetGPUUsage()`, `GetCurrentFPS()`, `GetGPUTemperature()` возвращали 0f. Теперь используют LibreHardwareMonitor + WMI
- **RegistryManager.RestoreRegistryKeyAsync** — игнорировал переданный keyPath, восстанавливал последний backup. Теперь использует manifest.json для точного восстановления
- **MemoryManager.ClearStandbyMemoryAsync** — чистил память своего процесса. Теперь использует `NtSetSystemInformation(MemoryPurgeStandbyList)`
- **GameDetector** — `DriveInfo.DriveType` не отличал SSD от HDD. Теперь WMI `Win32_DiskDrive` + `MSFT_PhysicalDisk` → HDD/SSD/NVMe
- **AutoOptimizationService** — игнорировал настройку `AutoOptimizeOnlyInGame`. Теперь проверяет `IsGameRunningAsync()`
- **PerformanceMonitor.UpdateMetricsAsync** — мог запускаться повторно. Теперь `SemaphoreSlim(1,1)` с `WaitAsync(0)`

#### Архитектура
- Все `Thread.Sleep()` → `await Task.Delay()`
- Все `Task.Delay().Wait()` → `await Task.Delay()` (sync-over-async)
- Все пустые `catch {}` → логирование с Exception и stack trace
- `LoggerService` — Channel<T> + async writer вместо Task.Run + lock
- `ILogger<T>` + `ILoggerService` — единая система логирования
- Интерфейсы разделены на отдельные файлы
- Удалён дубликат `DiskOptimizationResult.cs`
- Удалён неиспользуемый `IDiskOptimizer`

#### Новый функционал
- **SystemInfoDetector** — автоопределение CPU, GPU, RAM, экрана (без хардкода)
- **DiagnosticsService** — полная диагностика системы (12+ проверок)
- **GTA V Settings Analyzer** — анализ settings.xml с рекомендациями
- **PC Readiness Score** — оценка готовности ПК (CPU/GPU/RAM/Storage/Network → /100)
- **BenchmarkService** — бенчмарк с замером FPS, Frametime, стуттеров
- **Before/After Comparison** — сравнение производительности до и после оптимизации

#### UI/UX
- Полностью переработанный интерфейс в стиле Fluent Design / Gaming UI
- 6 вкладок: Оптимизация, Мониторинг, Диагностика, Бенчмарк, Логи, Настройки
- Тёмная тема с Indigo accent (#6366F1)
- Градиентные карточки, кастомные кнопки, toggle switches
- Цветные индикаторы статуса, анимации

#### Безопасность
- Backup реестра перед каждым изменением
- Защита системных процессов
- Восстановление настроек в один клик
- Точки восстановления Windows

---

## [1.0.0] — Начальная версия

- Базовая оптимизация
- Мониторинг (с заглушками)
- Управление процессами
- Простой UI
