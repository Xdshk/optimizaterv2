using GTA5Optimizer.Models.Majestic;

namespace GTA5Optimizer.Core.Interfaces;

/// <summary>
/// Интерфейс анализатора Majestic RP
/// </summary>
public interface IMajesticAnalyzer
{
    Task<MajesticAnalysisResult> AnalyzeAsync();
}
