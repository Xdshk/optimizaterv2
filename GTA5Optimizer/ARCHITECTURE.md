# Архитектура GTA5 Optimizer

## Обзор

Приложение разделено на 4 слоя по принципу Clean Architecture:

```
┌─────────────────────────────────────────┐
│            Presentation (UI)            │
│        GTA5Optimizer.UI (.WPF)        │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────┴─────────────────────┐
│           Application (Services)        │
│    GTA5Optimizer.Services (.NET 8)     │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────┴─────────────────────┐
│            Business (Core)              │
│      GTA5Optimizer.Core (.NET 8)       │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────┴─────────────────────┐
│            Data Models (Models)         │
│    GTA5Optimizer.Models (.NET 8)       │
└─────────────────────────────────────────┘
```

## Слой Core (Интерфейсы)

### ISystemOptimizer
Главный интерфейс для оптимизации системы
- `ApplyOptimizationsAsync()` - применить оптимизации
- `RestoreDefaultsAsync()` - восстановить настройки

### IGameDetector
Обнаружение игры и Majestic RP
- Поиск пути установки через реестр
- Определение типа диска (HDD/SSD)
- Проверка версии игры

### IProcessManager
Управление процессами Windows
- Изменение приоритетов процессов
- Управление affinity
- Закрытие/приостановка процессов

### IMemoryManager
Оптимизация памяти
- Очистка standby memory
- Trim working set
- Мониторинг использования RAM

### IRegistryManager
Работа с реестром с безопасством
- Создание точек восстановления
- Резервное копирование ключей
- Изменение значений

### IPerformanceMonitor
Мониторинг производительности
- CPU, GPU, RAM, Disk метрики
- Анализ узких мест
- Event-система обновления

### ILoggerService
Логирование действий
- Запись в файл
- Фильтрация по уровню
- Получение записей

## Слой Services (Реализация)

### SystemOptimizer
Основной класс оптимизации:
1. Управление энергопланами Windows
2. Оптимизация приоритетов процессов
3. Управление фоновыми процессами
4. Оптимизация Windows Services
5. Сетевая оптимизация

### ProcessManager
Реализует Windows API через P/Invoke:
- `SetPriorityClass` для изменения приоритета
- `SetProcessAffinityMask` для привязки к ядрам
- `SuspendProcess`/`ResumeProcess` для управления

### MemoryManager
Работа с памятью через:
- `EmptyWorkingSet` для очистки
- Performance Counters для мониторинга
- WMI для получения информации

### RegistryManager
Безопасная работа с реестром:
- PowerShell для точек восстановления
- reg.exe для экспорта/импорта
- Автоматическое резервное копирование

### GameDetector
Поиск установки игры:
- Поиск в реестре Windows
- Поиск в стандартных путях
- Определение типа носителя

### PerformanceMonitor
Сбор метрик через:
- PerformanceCounter для CPU/RAM
- WMI для температуры
- Hardware.Info для GPU

### AutoOptimizationService
Фоновый сервис:
- Периодическая проверка каждые 30 секунд
- Автоматическая очистка при необходимости
- Управление фоновыми процессами

### MajesticAnalyzer
Специализированный анализ:
- Оценка FPS
- Выявление причин просадок
- Рекомендации по оптимизации

## Слой Models (Данные)

### Основные модели:
- `OptimizationProfile` - типы профилей (Everyday, RPMode, MassiveOnline, MaximumFPS)
- `ProfileConfig` - настройки профиля
- `GameInfo` - информация об игре
- `MajesticInfo` - информация о Majestic RP
- `PerformanceMetrics` - метрики производительности
- `BottleneckAnalysis` - анализ узких мест
- `OptimizationResult` - результат оптимизации
- `AppSettings` - настройки приложения

## Слой UI (WPF)

### Архитектура MVVM:
- `MainWindowViewModel` - основной ViewModel
- `MonitorViewModel` - мониторинг
- `LogsViewModel` - логи
- `SettingsViewModel` - настройки
- `ProfileViewModel` - профили

### Features UI:
- Темная тема (#121212)
- Акцентные цвета (#00d4ff)
- Плавные анимации
- Real-time обновление метрик

## Профили оптимизации

### Everyday Mode
- Минимальные изменения
- Максимальная совместимость
- Только критические оптимизации

### RP Mode (по умолчанию)
- High Performance энергоплан
- Приоритеты для GTA5.exe и MajesticRP
- Закрытие браузеров и Discord
- Автоочистка памяти при 75%+

### Massive Online Mode
- Агрессивная очистка памяти
- Realtime приоритеты
- Частые интервалы оптимизации

### Maximum FPS Mode
- Все ресурсы в игру
- Блокировка на P-ядра
- Максимальные частоты

## Безопасность

1. **Точки восстановления** перед каждой оптимизацией
2. **Резервные копии реестра** ключей
3. **Защита системных процессов**
4. **Необходимость администратора**

## Требования к ПК

- Windows 10/11 x64
- .NET 8.0 Desktop Runtime
- Intel Core i5-12400F / RTX 3060 12GB / 16GB RAM
- GTA V на HDD (предупреждение)
- Majestic RP на SSD (рекомендуется)