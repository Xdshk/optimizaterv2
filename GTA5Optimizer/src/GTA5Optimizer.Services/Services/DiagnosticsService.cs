using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Monitoring;
using Microsoft.Extensions.Logging;
using System.Management;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Diagnostics;
using GTA5DriveType = GTA5Optimizer.Models.Enums.DriveType;

namespace GTA5Optimizer.Services.Services;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly ILogger<DiagnosticsService> _logger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IGameDetector _gameDetector;
    private readonly SystemInfoDetector _systemInfo;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    public DiagnosticsService(
        ILogger<DiagnosticsService> logger,
        IPerformanceMonitor performanceMonitor,
        IGameDetector gameDetector,
        SystemInfoDetector systemInfo)
    {
        _logger = logger;
        _performanceMonitor = performanceMonitor;
        _gameDetector = gameDetector;
        _systemInfo = systemInfo;
    }

    public async Task<DiagnosticsResult> RunFullDiagnosticsAsync(CancellationToken ct = default)
    {
        var result = new DiagnosticsResult();
        var metrics = await _performanceMonitor.GetCurrentMetricsAsync();
        var gameInfo = await _gameDetector.DetectGameAsync();

        // CPU temperature check
        if (metrics.CPUTemperature > 85)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "CPU_TEMP_HIGH",
                Title = "Перегрев CPU",
                Description = $"Температура CPU: {metrics.CPUTemperature:F0}°C. Критический порог: 85°C.",
                Recommendation = "Проверьте систему охлаждения, очистите от пыли, замените термопасту.",
                Severity = DiagnosticSeverity.Critical,
                Category = "Thermal"
            });
        }
        else if (metrics.CPUTemperature > 75)
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "CPU_TEMP_ELEVATED",
                Title = "Повышенная температура CPU",
                Description = $"Температура CPU: {metrics.CPUTemperature:F0}°C.",
                Recommendation = "Убедитесь в хорошей вентиляции корпуса.",
                Severity = DiagnosticSeverity.Warning,
                Category = "Thermal"
            });
        }

        // GPU temperature check
        if (metrics.GPUTemperature > 83)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "GPU_TEMP_HIGH",
                Title = "Перегрев GPU",
                Description = $"Температура GPU: {metrics.GPUTemperature:F0}°C. Критический порог: 83°C.",
                Recommendation = "Проверьте охлаждение видеокарты, снизьте настройки графики.",
                Severity = DiagnosticSeverity.Critical,
                Category = "Thermal"
            });
        }

        // RAM usage check
        if (metrics.RAMUsagePercent > 90)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "RAM_CRITICAL",
                Title = "Критическая нехватка RAM",
                Description = $"Используется {metrics.RAMUsagePercent:F0}% RAM. Свободно: {metrics.AvailableRAM / (1024 * 1024):F0} MB.",
                Recommendation = "Закройте фоновые приложения или выполните очистку памяти.",
                Severity = DiagnosticSeverity.Critical,
                Category = "RAM"
            });
        }
        else if (metrics.RAMUsagePercent > 80)
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "RAM_HIGH",
                Title = "Высокое потребление RAM",
                Description = $"Используется {metrics.RAMUsagePercent:F0}% RAM.",
                Recommendation = "Закройте ненужные приложения для освобождения памяти.",
                Severity = DiagnosticSeverity.Warning,
                Category = "RAM"
            });
        }

        // VRAM check
        if (metrics.GPUMemoryUsagePercent > 90)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "VRAM_CRITICAL",
                Title = "Критическая нехватка VRAM",
                Description = $"Используется {metrics.GPUMemoryUsagePercent:F0}% VRAM ({metrics.GPUMemoryUsed / (1024 * 1024):F0} MB из {metrics.GPUMemoryTotal / (1024 * 1024):F0} MB).",
                Recommendation = "Снизьте качество текстур и разрешение в настройках GTA V.",
                Severity = DiagnosticSeverity.Critical,
                Category = "GPU"
            });
        }
        else if (metrics.GPUMemoryUsagePercent > 75)
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "VRAM_HIGH",
                Title = "Высокое потребление VRAM",
                Description = $"Используется {metrics.GPUMemoryUsagePercent:F0}% VRAM.",
                Recommendation = "Рассмотрите снижение качества текстур.",
                Severity = DiagnosticSeverity.Warning,
                Category = "GPU"
            });
        }

        // Disk check
        if (gameInfo.DriveType == GTA5DriveType.HDD)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "GAME_ON_HDD",
                Title = "GTA V установлена на HDD",
                Description = "Игра установлена на жёстком диске. Это вызывает долгую загрузку текстур и просадки FPS.",
                Recommendation = "Перенесите GTA V на SSD для значительного улучшения производительности.",
                Severity = DiagnosticSeverity.Critical,
                Category = "Disk"
            });
        }

        // Disk space check
        if (gameInfo.DriveFreeSpace < 10L * 1024 * 1024 * 1024) // < 10 GB
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "DISK_SPACE_LOW",
                Title = "Мало места на диске",
                Description = $"Свободно на диске с игрой: {gameInfo.DriveFreeSpace / (1024 * 1024 * 1024):F1} GB.",
                Recommendation = "Освободите место на диске. Рекомендуется минимум 20 GB свободного места.",
                Severity = DiagnosticSeverity.Warning,
                Category = "Disk"
            });
        }

        // Disk activity check
        if (metrics.DiskActiveTimePercent > 80)
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "DISK_BUSY",
                Title = "Диск перегружен",
                Description = $"Активность диска: {metrics.DiskActiveTimePercent:F0}%. Это может вызывать подтормаживания.",
                Recommendation = "Закройте приложения, активно использующие диск.",
                Severity = DiagnosticSeverity.Warning,
                Category = "Disk"
            });
        }

        // PageFile check
        CheckPageFile(result);

        // Background processes check
        CheckBackgroundProcesses(result);

        // FPS stability check
        if (metrics.CurrentFPS > 0 && metrics.CurrentFPS < 30)
        {
            result.Issues.Add(new DiagnosticIssue
            {
                Id = "FPS_LOW",
                Title = "Очень низкий FPS",
                Description = $"Текущий FPS: {metrics.CurrentFPS:F0}. Комфортный порог: 60 FPS.",
                Recommendation = "Запустите оптимизацию или снизьте настройки графики в GTA V.",
                Severity = DiagnosticSeverity.Critical,
                Category = "Game"
            });
        }

        // Frametime check
        if (metrics.FrameTimeMs > 33 && metrics.CurrentFPS > 0)
        {
            result.Warnings.Add(new DiagnosticIssue
            {
                Id = "FRAMETIME_HIGH",
                Title = "Высокий Frametime",
                Description = $"Frametime: {metrics.FrameTimeMs}ms. Цель: < 16ms для 60 FPS.",
                Recommendation = "Снизьте настройки графики или закройте фоновые приложения.",
                Severity = DiagnosticSeverity.Warning,
                Category = "Game"
            });
        }

        // 1% low check
        if (metrics.OnePercentLow > 0 && metrics.CurrentFPS > 0)
        {
            var ratio = metrics.OnePercentLow / metrics.CurrentFPS;
            if (ratio < 0.5)
            {
                result.Warnings.Add(new DiagnosticIssue
                {
                    Id = "FPS_STUTTER",
                    Title = "Нестабильный FPS (микрофризы)",
                    Description = $"1% Low: {metrics.OnePercentLow:F0} FPS при среднем {metrics.CurrentFPS:F0} FPS. Соотношение: {ratio:P0}.",
                    Recommendation = "Проверьте температуры, закройте фоновые приложения, обновите драйверы.",
                    Severity = DiagnosticSeverity.Warning,
                    Category = "Game"
                });
            }
        }

        // Calculate total score
        int penalty = result.Issues.Count(i => i.Severity == DiagnosticSeverity.Critical) * 15
                     + result.Issues.Count(i => i.Severity == DiagnosticSeverity.Warning) * 5
                     + result.Warnings.Count * 3;
        result.TotalScore = Math.Max(0, 100 - penalty);

        _logger.LogInformation("Diagnostics completed: {Issues} issues, {Warnings} warnings, score: {Score}",
            result.Issues.Count, result.Warnings.Count, result.TotalScore);

        return result;
    }

    public async Task<GtaVSettingsAnalysis> AnalyzeGtaVSettingsAsync(string gtaVPath, CancellationToken ct = default)
    {
        var analysis = new GtaVSettingsAnalysis();

        // GTA V stores settings.xml in Documents\Rockstar Games\GTA V\, NOT in the game install dir
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var docSettingsPath = Path.Combine(documentsPath, "Rockstar Games", "GTA V", "settings.xml");
        var gameSettingsPath = Path.Combine(gtaVPath, "settings.xml");

        // Try Documents path first (where GTA V actually stores settings), then game dir
        var settingsPath = File.Exists(docSettingsPath) ? docSettingsPath : gameSettingsPath;
        analysis.SettingsPath = settingsPath;

        if (!File.Exists(settingsPath))
        {
            analysis.Issues.Add(new SettingsIssue
            {
                SettingName = "settings.xml",
                CurrentValue = "Not found",
                RecommendedValue = "N/A",
                Description = $"Файл settings.xml не найден. Проверены пути:{Environment.NewLine}  {docSettingsPath}{Environment.NewLine}  {gameSettingsPath}{Environment.NewLine}Возможно, игра ещё не запускалась.",
                Severity = SettingsIssueSeverity.Info
            });
            analysis.Recommendations.Add(new SettingsRecommendation
            {
                Title = "Запустить GTA V один раз",
                Description = "После первого запуска игра создаст settings.xml в папке Documents\\Rockstar Games\\GTA V\\, и анализатор сможет проверить настройки графики.",
                Action = "Запустите GTA V, зайдите в меню настроек и выйдите",
                ExpectedFpsGain = 0
            });
            analysis.PerformanceScore = 50;
            return analysis;
        }

        try
        {
            var doc = await Task.Run(() => XDocument.Load(settingsPath), ct);
            var gfx = doc.Root?.Element("GFX") ?? doc.Root?.Element("graphics");

            if (gfx == null)
            {
                analysis.Issues.Add(new SettingsIssue
                {
                    SettingName = "GFX",
                    CurrentValue = "Not found",
                    RecommendedValue = "N/A",
                    Description = "Секция GFX не найдена в settings.xml.",
                    Severity = SettingsIssueSeverity.Info
                });
                analysis.Recommendations.Add(new SettingsRecommendation
                {
                    Title = "Проверить settings.xml",
                    Description = "Файл существует, но не содержит секцию GFX с настройками графики.",
                    Action = "Откройте settings.xml и убедитесь, что есть секция GFX",
                    ExpectedFpsGain = 0
                });
                analysis.PerformanceScore = 50;
                return analysis;
            }

            // GTA V settings.xml uses string values: "Off", "Low", "Medium", "High", "Very High", "Ultra"
            // and also numeric "0"-"4" for some settings. We check the actual string value.
            // For max FPS: lower is better (Low/Off optimal), except FXAA (should be On).

            CheckGtaVSetting(gfx, "TextureQuality", "Качество текстур", analysis,
                optimalValue: "Normal",
                worseValues: new[] { "High", "Very High", "Ultra" },
                recommendation: "Уменьшите до Normal для +10-20 FPS");

            CheckGtaVSetting(gfx, "ShadowQuality", "Качество теней", analysis,
                optimalValue: "Normal",
                worseValues: new[] { "High", "Very High", "Ultra", "Soft", "Softest", "Nvidia PCSS" },
                recommendation: "Уменьшите до Normal для +5-15 FPS");

            CheckGtaVSetting(gfx, "ReflectionQuality", "Качество отражений", analysis,
                optimalValue: "Low",
                worseValues: new[] { "Medium", "High", "Very High", "Ultra" },
                recommendation: "Уменьшите до Low для +5-10 FPS");

            CheckGtaVSetting(gfx, "MSAA", "MSAA", analysis,
                optimalValue: "Off",
                worseValues: new[] { "2x", "4x", "8x" },
                recommendation: "Отключите MSAA для +10-20 FPS");

            CheckGtaVSetting(gfx, "FXAA", "FXAA", analysis,
                optimalValue: "On",
                worseValues: new[] { "Off" },
                recommendation: "FXAA почти бесплатен, включите");

            CheckGtaVSetting(gfx, "PopulationDensity", "Плотность населения", analysis,
                optimalValue: "50",
                worseValues: new[] { "75", "100" },
                recommendation: "Уменьшите до 50% для +5-10 FPS в городах");

            CheckGtaVSetting(gfx, "DistanceScaling", "Дальность прорисовки", analysis,
                optimalValue: "50",
                worseValues: new[] { "75", "100" },
                recommendation: "Уменьшите до 50% для +5-10 FPS");

            CheckGtaVSetting(gfx, "VSync", "VSync", analysis,
                optimalValue: "Off",
                worseValues: new[] { "On", "1", "2", "Adaptive" },
                recommendation: "Отключите VSync для снятия ограничения FPS");

            CheckGtaVSetting(gfx, "GrassQuality", "Качество травы", analysis,
                optimalValue: "Low",
                worseValues: new[] { "Medium", "High", "Very High", "Ultra" },
                recommendation: "Уменьшите до Low для +5-15 FPS");

            CheckGtaVSetting(gfx, "PostFX", "Post-обработка", analysis,
                optimalValue: "Low",
                worseValues: new[] { "Medium", "High", "Very High", "Ultra" },
                recommendation: "Уменьшите до Low для +3-8 FPS");

            CheckGtaVSetting(gfx, "AnisotropicFiltering", "Анизотропная фильтрация", analysis,
                optimalValue: "Off",
                worseValues: new[] { "2x", "4x", "8x", "16x", "High", "Medium" },
                recommendation: "Отключите для +2-3 FPS (незаметно на низких настройках)");

            // Extended settings
            CheckGtaVSetting(gfx, "Tessellation", "Тесселяция", analysis,
                optimalValue: "Off",
                worseValues: new[] { "On", "High", "Medium", "Very High" },
                recommendation: "Отключите тесселяцию для +5-10 FPS");

            CheckGtaVSetting(gfx, "ShaderQuality", "Качество шейдеров", analysis,
                optimalValue: "Normal",
                worseValues: new[] { "High", "Very High" },
                recommendation: "Уменьшите до Normal для +3-5 FPS");

            CheckGtaVSetting(gfx, "WaterQuality", "Качество воды", analysis,
                optimalValue: "Low",
                worseValues: new[] { "Medium", "High", "Very High", "Ultra" },
                recommendation: "Уменьшите до Low для +3-5 FPS");

            if (analysis.Recommendations.Count == 0)
            {
                analysis.Recommendations.Add(new SettingsRecommendation
                {
                    Title = "Настройки уже оптимизированы",
                    Description = "Проверенные параметры GTA V уже соответствуют профилю максимального FPS.",
                    Action = "Оставить текущие настройки",
                    ExpectedFpsGain = 0
                });
            }

            // Calculate performance score
            int issueCount = analysis.Issues.Count(i => i.Severity == SettingsIssueSeverity.Performance);
            int criticalCount = analysis.Issues.Count(i => i.Severity == SettingsIssueSeverity.Critical);
            analysis.PerformanceScore = Math.Max(0, 100 - criticalCount * 20 - issueCount * 10);

            _logger.LogInformation("GTA V settings analysis: {Issues} issues, score: {Score}",
                analysis.Issues.Count, analysis.PerformanceScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GTA V settings.xml");
            analysis.Issues.Add(new SettingsIssue
            {
                SettingName = "ParseError",
                CurrentValue = "Error",
                RecommendedValue = "N/A",
                Description = $"Ошибка чтения settings.xml: {ex.Message}",
                Severity = SettingsIssueSeverity.Info
            });
        }

        return analysis;
    }

    public async Task<PcReadinessScore> CalculateReadinessScoreAsync(CancellationToken ct = default)
    {
        var score = new PcReadinessScore();
        var hw = _systemInfo.DetectHardwareProfile();
        var metrics = await _performanceMonitor.GetCurrentMetricsAsync();

        score.CpuName = hw.CPUName;
        score.GpuName = hw.GPUName;
        score.TotalRAM_GB = hw.TotalRAM_GB;

        // CPU Score (0-100)
        score.CpuScore = CalculateCpuScore(hw.CPUName);

        // GPU Score (0-100)
        score.GpuScore = CalculateGpuScore(hw.GPUName);

        // RAM Score
        score.RamScore = CalculateRamScore(hw.TotalRAM_GB);

        // Storage Score
        var gameInfo = await _gameDetector.DetectGameAsync();
        score.StorageScore = gameInfo.DriveType switch
        {
            GTA5DriveType.NVMe => 100,
            GTA5DriveType.SSD => 85,
            GTA5DriveType.HDD => 40,
            _ => 50
        };

        // Network Score
        score.NetworkScore = metrics.CurrentPing switch
        {
            0 => 50, // Unknown
            <= 20 => 100,
            <= 50 => 90,
            <= 80 => 75,
            <= 120 => 60,
            _ => 40
        };

        // Overall score (weighted)
        score.OverallScore = (int)Math.Round(
            score.CpuScore * 0.25 +
            score.GpuScore * 0.30 +
            score.RamScore * 0.20 +
            score.StorageScore * 0.15 +
            score.NetworkScore * 0.10);

        // Strengths
        if (score.CpuScore >= 80) score.Strengths.Add("Процессор отлично подходит для GTA V");
        if (score.GpuScore >= 80) score.Strengths.Add("Видеокарта обеспечит высокий FPS");
        if (score.RamScore >= 80) score.Strengths.Add("Достаточно оперативной памяти");
        if (score.StorageScore >= 85) score.Strengths.Add("SSD обеспечит быструю загрузку текстур");
        if (score.NetworkScore >= 90) score.Strengths.Add("Отличное сетевое соединение");

        // Weaknesses
        if (score.CpuScore < 60) score.Weaknesses.Add("Процессор может быть узким местом");
        if (score.GpuScore < 60) score.Weaknesses.Add("Видеокарта может не справиться с высокими настройками");
        if (score.RamScore < 60) score.Weaknesses.Add("Нехватка RAM может вызывать фризы");
        if (score.StorageScore < 60) score.Weaknesses.Add("HDD вызывает долгую загрузку текстур и просадки");
        if (score.NetworkScore < 60) score.Weaknesses.Add("Высокий пинг может вызывать лаги в онлайне");

        score.Summary = score.OverallScore switch
        {
            >= 90 => "Отлично! Ваш ПК идеально подходит для Majestic RP.",
            >= 75 => "Хорошо. Ваш ПК справится с комфортной игрой.",
            >= 60 => "Удовлетворительно. Возможны просадки FPS в сложных сценах.",
            >= 40 => "Ниже среднего. Рекомендуется оптимизация и снижение настроек.",
            _ => "Требуется серьёзная оптимизация или апгрейд железа."
        };

        _logger.LogInformation("PC Readiness Score: {Score}/100 (CPU={CPU}, GPU={GPU}, RAM={RAM}, SSD={SSD}, Net={Net})",
            score.OverallScore, score.CpuScore, score.GpuScore, score.RamScore, score.StorageScore, score.NetworkScore);

        return score;
    }

    #region Private helpers

    private static int CalculateCpuScore(string cpuName)
    {
        var cpu = cpuName.ToLowerInvariant();

        // High-end CPUs
        if (cpu.Contains("i9") || cpu.Contains("ryzen 9") || cpu.Contains("i7-13") || cpu.Contains("i7-14") ||
            cpu.Contains("ryzen 7 7800x") || cpu.Contains("ryzen 7 9"))
            return 95;

        // Upper mid-range
        if (cpu.Contains("i7") || cpu.Contains("ryzen 7"))
            return 85;

        // Mid-range
        if (cpu.Contains("i5-12") || cpu.Contains("i5-13") || cpu.Contains("i5-14") ||
            cpu.Contains("ryzen 5 5600") || cpu.Contains("ryzen 5 7600") || cpu.Contains("ryzen 5 8"))
            return 80;

        // Lower mid-range
        if (cpu.Contains("i5") || cpu.Contains("ryzen 5"))
            return 70;

        // Older/entry-level
        if (cpu.Contains("i3") || cpu.Contains("ryzen 3"))
            return 55;

        return 60; // Unknown
    }

    private static int CalculateGpuScore(string gpuName)
    {
        var gpu = gpuName.ToLowerInvariant();

        // High-end
        if (gpu.Contains("rtx 4090") || gpu.Contains("rtx 4080") || gpu.Contains("rtx 5090") || gpu.Contains("rtx 5080"))
            return 100;
        if (gpu.Contains("rtx 4070") || gpu.Contains("rtx 5070") || gpu.Contains("rx 7900"))
            return 95;

        // Upper mid-range
        if (gpu.Contains("rtx 4060") || gpu.Contains("rtx 3070") || gpu.Contains("rtx 5060") || gpu.Contains("rx 7800"))
            return 85;

        // Mid-range
        if (gpu.Contains("rtx 3060") || gpu.Contains("rtx 2070") || gpu.Contains("rtx 4060") ||
            gpu.Contains("rx 6700") || gpu.Contains("rx 7600"))
            return 75;

        // Lower mid-range
        if (gpu.Contains("rtx 3050") || gpu.Contains("rtx 2060") || gpu.Contains("gtx 1660") || gpu.Contains("rx 6600"))
            return 65;

        // Entry-level
        if (gpu.Contains("gtx 1050") || gpu.Contains("gtx 1650") || gpu.Contains("rx 6500"))
            return 45;

        return 55; // Unknown
    }

    private static int CalculateRamScore(long totalRAM_GB)
    {
        return totalRAM_GB switch
        {
            >= 32 => 100,
            >= 16 => 85,
            >= 12 => 70,
            >= 8 => 55,
            _ => 35
        };
    }

    private static void CheckSetting(XElement gfx, string settingName, string displayName,
        GtaVSettingsAnalysis analysis, string optimalValue, string highImpact)
    {
        var element = gfx.Element(settingName);
        if (element == null) return;

        var currentValue = element.Value;
        if (currentValue != optimalValue)
        {
            analysis.Issues.Add(new SettingsIssue
            {
                SettingName = displayName,
                CurrentValue = currentValue,
                RecommendedValue = optimalValue,
                Description = $"{displayName} установлено на {currentValue}, рекомендуется {optimalValue}.",
                Severity = SettingsIssueSeverity.Performance
            });

            analysis.Recommendations.Add(new SettingsRecommendation
            {
                Title = $"Оптимизировать {displayName}",
                Description = highImpact,
                Action = $"Установить {displayName} = {optimalValue}",
                ExpectedFpsGain = 5 // Conservative estimate
            });
        }
    }

    private void GenerateRecommendations(GtaVSettingsAnalysis analysis)
    {
        if (!analysis.Issues.Any(i => i.SettingName == "VSync" && i.CurrentValue != "0"))
            return;

        analysis.Recommendations.Add(new SettingsRecommendation
        {
            Title = "Отключить VSync",
            Description = "VSync ограничивает FPS и добавляет инпут-лаг. Рекомендуется отключить.",
            Action = "Установить VSync = 0",
            ExpectedFpsGain = 10
        });
    }

    private void CheckPageFile(DiagnosticsResult result)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AllocatedBaseSize FROM Win32_PageFileSetting");
            bool hasPageFile = false;
            foreach (ManagementObject? mo in searcher.Get())
            {
                hasPageFile = true;
                var size = Convert.ToInt32(mo["AllocatedBaseSize"] ?? 0);
                if (size < 4096) // Less than 4GB
                {
                    result.Warnings.Add(new DiagnosticIssue
                    {
                        Id = "PAGEFILE_SMALL",
                        Title = "Маленький файл подкачки",
                        Description = $"Размер PageFile: {size} MB. Рекомендуется минимум 4096 MB.",
                        Recommendation = "Увеличьте размер файла подкачки в настройках системы.",
                        Severity = DiagnosticSeverity.Warning,
                        Category = "RAM"
                    });
                }
            }

            if (!hasPageFile)
            {
                result.Issues.Add(new DiagnosticIssue
                {
                    Id = "PAGEFILE_DISABLED",
                    Title = "Файл подкачки отключён",
                    Description = "PageFile отключён. Это может вызывать краши при нехватке RAM.",
                    Recommendation = "Включите файл подкачки. Рекомендуемый размер: 4096-8192 MB.",
                    Severity = DiagnosticSeverity.Critical,
                    Category = "RAM"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check PageFile settings");
        }
    }

    private void CheckBackgroundProcesses(DiagnosticsResult result)
    {
        try
        {
            var heavyProcesses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["chrome"] = "Google Chrome",
                ["msedge"] = "Microsoft Edge",
                ["opera"] = "Opera",
                ["brave"] = "Brave Browser",
                ["teams"] = "Microsoft Teams",
                ["onedrive"] = "OneDrive",
                ["dropbox"] = "DropBox",
                ["epicgameslauncher"] = "Epic Games Launcher",
                ["origin"] = "EA Origin",
                ["battle.net"] = "Battle.net",
                ["geforce experience"] = "NVIDIA GeForce Experience"
            };

            var foundHeavy = new List<string>();
            foreach (var kvp in heavyProcesses)
            {
                if (Process.GetProcessesByName(kvp.Key).Length > 0)
                    foundHeavy.Add(kvp.Value);
            }

            if (foundHeavy.Count >= 3)
            {
                result.Warnings.Add(new DiagnosticIssue
                {
                    Id = "BG_PROCESSES_HEAVY",
                    Title = "Много фоновых процессов",
                    Description = $"Обнаружены ресурсоёмкие процессы: {string.Join(", ", foundHeavy)}.",
                    Recommendation = "Закройте ненужные приложения для освобождения ресурсов.",
                    Severity = DiagnosticSeverity.Warning,
                    Category = "CPU"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check background processes");
        }
    }

    #endregion
}
