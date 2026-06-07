using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для управления профилями
/// </summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileManager _profileManager;

    [ObservableProperty]
    private ObservableCollection<ProfileConfig> _profiles = new();

    [ObservableProperty]
    private ProfileConfig? _selectedProfile;

    public ProfileViewModel(IProfileManager profileManager)
    {
        _profileManager = profileManager;
        _ = LoadProfilesAsync();
    }

    [RelayCommand]
    private async Task LoadProfilesAsync()
    {
        var profiles = await _profileManager.GetAvailableProfilesAsync();
        Profiles = new ObservableCollection<ProfileConfig>(profiles);
    }

    [RelayCommand]
    private async Task ApplyProfileAsync()
    {
        if (SelectedProfile != null)
        {
            await _profileManager.ApplyProfileAsync(SelectedProfile.Profile);
        }
    }
}