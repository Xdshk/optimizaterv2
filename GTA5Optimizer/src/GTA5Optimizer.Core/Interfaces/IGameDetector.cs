using GTA5Optimizer.Models.Game;

namespace GTA5Optimizer.Core.Interfaces;

public interface IGameDetector
{
    Task<GameInfo> DetectGameAsync();
    Task<MajesticInfo> DetectMajesticRPAsync();
    Task<bool> IsGameRunningAsync();
}
