namespace GTA5Optimizer.Models.Enums;

public enum OptimizationProfile
{
    /// <summary>
    /// Режим для повседневной работы — минимальные изменения, максимальная совместимость
    /// </summary>
    Everyday = 0,

    /// <summary>
    /// Оптимизированный баланс для RP — приоритет стабильности FPS и плавности
    /// </summary>
    RPMode = 1,

    /// <summary>
    /// Для массовых ивентов (100+ игроков) — агрессивная очистка памяти и приоритеты
    /// </summary>
    MassiveOnline = 2,

    /// <summary>
    /// Максимальный FPS — все ресурсы в игру, отключение всего лишнего
    /// </summary>
    MaximumFPS = 3
}